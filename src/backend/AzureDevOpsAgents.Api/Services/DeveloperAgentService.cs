using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Data.Entities;
using AzureDevOpsAgents.Api.Hubs;
using AzureDevOpsAgents.Api.Models;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AzureDevOpsAgents.Api.Services;

/// <summary>
/// Developer sub-agent: spawns a per-job Copilot CLI session scoped to the
/// cloned repository directory, attempts to complete a work item, and either
/// opens a pull request (success) or adds a comment to the work item (failure).
/// </summary>
public class DeveloperAgentService(
    IServiceScopeFactory scopeFactory,
    IHubContext<AgentHub> hub,
    McpServerManager mcp,
    TokenEncryptionService encryption,
    IConfiguration configuration,
    ILogger<DeveloperAgentService> logger)
{
    /// <summary>
    /// Start a developer job asynchronously. Returns the created AgentJob immediately;
    /// actual work runs on a background task.
    /// </summary>
    public async Task<AgentJob> StartJobAsync(
        Guid sessionId,
        string workItemId,
        string workItemTitle,
        string repoName,
        CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.ChatSessions
            .Include(s => s.Connection)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        var repo = await db.Repos
            .FirstOrDefaultAsync(r => r.ConnectionId == session.ConnectionId && r.RepoName == repoName, ct);

        var job = new AgentJob
        {
            SessionId      = sessionId,
            RepoId         = repo?.Id,
            AgentType      = AgentType.Developer,
            Status         = JobStatus.Queued,
            WorkItemId     = workItemId,
            WorkItemTitle  = workItemTitle,
            Prompt         = $"Complete work item #{workItemId}: {workItemTitle}"
        };

        db.AgentJobs.Add(job);
        await db.SaveChangesAsync(ct);

        // Notify frontend
        await hub.Clients.Group($"session-{sessionId}").SendAsync(
            "AgentStatus",
            new AgentStatusEvent(sessionId, job.Id, "Developer", "Queued", $"Work item #{workItemId}: {workItemTitle}"),
            ct);

        // Run in background; service handles its own scope/lifetime.
        _ = RunJobAsync(job.Id);

        return job;
    }

    private async Task RunJobAsync(Guid jobId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.AgentJobs
            .Include(j => j.Session)
                .ThenInclude(s => s.Connection)
            .Include(j => j.Repo)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job is null)
        {
            logger.LogError("Job {JobId} not found when RunJobAsync started", jobId);
            return;
        }

        var connection = job.Session.Connection;
        var orgName    = ExtractOrgName(connection.OrganizationUrl);
        var accessToken = connection.AccessToken is not null
            ? encryption.Decrypt(connection.AccessToken)
            : null;

        if (accessToken is null)
        {
            await FailJobAsync(db, job, "No access token stored for this connection.");
            return;
        }

        var repoPath = job.Repo?.LocalClonePath;
        if (repoPath is null || !Directory.Exists(repoPath))
        {
            await FailJobAsync(db, job, $"Repository '{job.Repo?.RepoName ?? "unknown"}' is not cloned on the server.");
            return;
        }

        job.Status    = JobStatus.Running;
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await hub.Clients.Group($"session-{job.SessionId}").SendAsync(
            "AgentStatus",
            new AgentStatusEvent(job.SessionId, job.Id, "Developer", "Running",
                $"Working on work item #{job.WorkItemId}..."));

        var mcpConfigPath = mcp.WriteMcpConfig($"dev-{job.Id}", orgName, accessToken, AgentRole.Developer);
        var githubToken   = configuration["GitHub:Token"];

        try
        {
            await using var client = new CopilotClient(new CopilotClientOptions
            {
                Cwd         = repoPath,
                GitHubToken = githubToken,
                CliArgs     = ["--additional-mcp-config", $"@{mcpConfigPath}", "--add-github-mcp-toolset", "memory"],
                LogLevel    = "warning"
            });

            await client.StartAsync();

            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                Streaming  = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                SystemMessage = new SystemMessageConfig
                {
                    Mode    = SystemMessageMode.Append,
                    Content = BuildDeveloperSystemMessage(connection.ProjectName, orgName, job.WorkItemId!, job.WorkItemTitle!)
                }
            });

            var log  = new System.Text.StringBuilder();
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var succeeded = false;
            var prUrl     = (string?)null;

            using var _ = session.On(async evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta when delta.Data.DeltaContent is not null:
                        log.Append(delta.Data.DeltaContent);
                        await hub.Clients.Group($"session-{job.SessionId}").SendAsync(
                            "AgentLog",
                            new AgentLogEvent(job.Id, delta.Data.DeltaContent));
                        break;

                    case AssistantMessageEvent msg:
                        var content = msg.Data.Content ?? string.Empty;
                        // Look for a PR URL in the final message
                        var prMatch = System.Text.RegularExpressions.Regex.Match(
                            content, @"https://dev\.azure\.com/.+?/_git/.+?/pullrequest/\d+");
                        if (prMatch.Success)
                        {
                            prUrl     = prMatch.Value;
                            succeeded = true;
                        }
                        else if (content.Contains("pull request", StringComparison.OrdinalIgnoreCase)
                              && content.Contains("created", StringComparison.OrdinalIgnoreCase))
                        {
                            succeeded = true;
                        }
                        break;

                    case SessionIdleEvent:
                        done.TrySetResult();
                        break;

                    case SessionErrorEvent err:
                        done.TrySetException(new Exception(err.Data.Message));
                        break;
                }
            });

            var prompt = $"""
                Complete work item #{job.WorkItemId}: "{job.WorkItemTitle}".

                Steps:
                1. Use the ADO MCP tools to get the full details of work item #{job.WorkItemId}.
                2. Implement the changes required in the repository at {repoPath}.
                3. Commit your changes with a meaningful message referencing #{job.WorkItemId}.
                4. Use the ADO MCP tools to create a pull request targeting the default branch.
                5. If you cannot complete the task (it's too complex, requirements are unclear, etc.),
                   use the ADO MCP tools to add a comment to work item #{job.WorkItemId} explaining
                   exactly what is missing or needs clarification, then output:
                   TASK_BLOCKED: <reason>
                """;

            await session.SendAsync(new MessageOptions { Prompt = prompt });

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            await done.Task.WaitAsync(timeout.Token);

            var logText = log.ToString();
            job.Log       = logText;
            job.UpdatedAt = DateTime.UtcNow;

            if (succeeded)
            {
                job.Status    = JobStatus.Succeeded;
                job.ResultUrl = prUrl;
                logger.LogInformation("Developer job {Job} succeeded with PR {Url}", job.Id, prUrl);
            }
            else if (logText.Contains("TASK_BLOCKED:"))
            {
                var reason = System.Text.RegularExpressions.Regex.Match(logText, @"TASK_BLOCKED:\s*(.+)").Groups[1].Value.Trim();
                await FailJobAsync(db, job, reason);
            }
            else
            {
                await FailJobAsync(db, job, "Task completed but no pull request was created.");
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Developer job {Job} threw an exception", job.Id);
            await FailJobAsync(db, job, ex.Message);
        }
        finally
        {
            mcp.CleanupConfig($"dev-{job.Id}");
        }

        // Notify frontend of final status
        await hub.Clients.Group($"session-{job.SessionId}").SendAsync(
            "AgentStatus",
            new AgentStatusEvent(job.SessionId, job.Id, "Developer", job.Status.ToString(),
                job.Status == JobStatus.Succeeded
                    ? $"Pull request created: {job.ResultUrl}"
                    : job.ErrorMessage));
    }

    private static async Task FailJobAsync(AppDbContext db, AgentJob job, string reason)
    {
        job.Status       = JobStatus.Failed;
        job.ErrorMessage = reason;
        job.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static string BuildDeveloperSystemMessage(string project, string org, string wiId, string wiTitle) => $"""
        You are the Developer agent for Azure DevOps project '{project}' in org '{org}'.
        You have full access to the cloned repository and ADO work item/repository MCP tools.
        Your goal is to implement work item #{wiId} ("{wiTitle}") and open a pull request.
        Write clean, idiomatic code. Follow existing conventions in the repository.
        If the task is ambiguous or too complex, add a comment to the work item via the MCP tools
        and output TASK_BLOCKED: <reason> so the Assistant can notify the user.
        """;

    private static string ExtractOrgName(string orgUrl)
    {
        var uri = new Uri(orgUrl);
        return uri.Segments.LastOrDefault()?.TrimEnd('/') ?? uri.Host;
    }
}
