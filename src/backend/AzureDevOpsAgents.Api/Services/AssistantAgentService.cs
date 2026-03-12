using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Data.Entities;
using AzureDevOpsAgents.Api.Hubs;
using AzureDevOpsAgents.Api.Models;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace AzureDevOpsAgents.Api.Services;

/// <summary>
/// Manages the long-lived CopilotClient for the Assistant agent.
/// Streams responses back to the frontend via SignalR.
/// Delegates coding tasks to DeveloperAgentService.
/// </summary>
public class AssistantAgentService(
    IServiceScopeFactory scopeFactory,
    IHubContext<AgentHub> hub,
    McpServerManager mcp,
    TokenEncryptionService encryption,
    IConfiguration configuration,
    ILogger<AssistantAgentService> logger) : IAsyncDisposable
{
    // One CopilotClient per AssistantAgentService instance; keyed on the connection
    private CopilotClient? _client;
    private string? _mcpConfigKey;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sessionLocks = new();

    private async Task<CopilotClient> GetClientAsync(string orgName, string accessToken, CancellationToken ct = default)
    {
        if (_client is not null) return _client;

        await _startLock.WaitAsync(ct);
        try
        {
            if (_client is not null) return _client;

            // Write MCP config once; reused for all sessions on this client
            _mcpConfigKey = $"assistant-{Guid.NewGuid():N}";
            var mcpConfigPath = mcp.WriteMcpConfig(_mcpConfigKey, orgName, accessToken, AgentRole.Assistant);

            var githubToken = configuration["GitHub:Token"];
            _client = new CopilotClient(new CopilotClientOptions
            {
                GitHubToken = githubToken,
                CliArgs     = ["--additional-mcp-config", $"@{mcpConfigPath}", "--add-github-mcp-toolset", "memory"],
                LogLevel    = "warning"
            });
            await _client.StartAsync();
            logger.LogInformation("Copilot CLI client started for Assistant agent (org: {Org})", orgName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Copilot CLI client for org {Org}", orgName);
            throw;
        }
        finally
        {
            _startLock.Release();
        }

        return _client;
    }

    /// <summary>
    /// Process a user message in the context of a chat session. Streams response
    /// chunks via SignalR to the session group and persists the final message to DB.
    /// </summary>
    public async Task ProcessMessageAsync(
        Guid sessionId,
        string userContent,
        CancellationToken ct = default)
    {
        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await sessionLock.WaitAsync(ct);

        var assistantJobId = Guid.NewGuid();

        await hub.Clients.Group($"session-{sessionId}").SendAsync(
            "AgentStatus",
            new AgentStatusEvent(sessionId, assistantJobId, "Assistant", "Queued", "Message queued"),
            CancellationToken.None);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
        var session = await db.ChatSessions
            .Include(s => s.Connection)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        var connection = session.Connection;
        var orgName = ExtractOrgName(connection.OrganizationUrl);
        var accessToken = connection.AccessToken is not null
            ? encryption.Decrypt(connection.AccessToken)
            : throw new InvalidOperationException("No access token for connection.");

        // Persist user message
        db.ChatMessages.Add(new ChatMessage
        {
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = userContent
        });
        await db.SaveChangesAsync(ct);

        await hub.Clients.Group($"session-{sessionId}").SendAsync(
            "AgentStatus",
            new AgentStatusEvent(sessionId, assistantJobId, "Assistant", "Running", "Thinking..."),
            CancellationToken.None);

        // Client is created once per service instance with the MCP config baked in
        var client = await GetClientAsync(orgName, accessToken, ct);

        // Resume an existing Copilot session, or create a new one.
        // Use "N" format (no hyphens) — the CLI rejects hyphens in session IDs.
        var cliSessionId = sessionId.ToString("N");
        CopilotSession copilotSession;
        try
        {
            copilotSession = await client.ResumeSessionAsync(cliSessionId, new ResumeSessionConfig
            {
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                SystemMessage = new SystemMessageConfig
                {
                    Mode    = SystemMessageMode.Append,
                    Content = BuildSystemMessage(connection.ProjectName, orgName)
                }
            });
        }
        catch(Exception e)
        {
            try
            {
            copilotSession = await client.CreateSessionAsync(new SessionConfig
            {
                SessionId = cliSessionId,
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                SystemMessage = new SystemMessageConfig
                {
                    Mode    = SystemMessageMode.Append,
                    Content = BuildSystemMessage(connection.ProjectName, orgName)
                }
            });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create or resume Copilot session for session {SessionId}", sessionId);
                throw;
            }
        }

        await using (copilotSession)
        {
            var assistantMessageId = Guid.NewGuid();
            var fullContent = new System.Text.StringBuilder();
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var _ = copilotSession.On(async evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta when delta.Data.DeltaContent is not null:
                        fullContent.Append(delta.Data.DeltaContent);
                        await hub.Clients.Group($"session-{sessionId}").SendAsync(
                            "MessageChunk",
                            new MessageChunkEvent(sessionId, assistantMessageId, delta.Data.DeltaContent, false),
                            ct);
                        break;

                    case AssistantMessageEvent msg:
                        // Final chunk
                        await hub.Clients.Group($"session-{sessionId}").SendAsync(
                            "MessageChunk",
                            new MessageChunkEvent(sessionId, assistantMessageId, string.Empty, true),
                            ct);
                        break;

                    case SessionIdleEvent:
                        done.TrySetResult();
                        break;

                    case SessionErrorEvent err:
                        done.TrySetException(new Exception(err.Data.Message));
                        break;
                }
            });

            try
            {
                await copilotSession.SendAsync(new MessageOptions { Prompt = userContent });

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                await done.Task.WaitAsync(linkedCts.Token);

                // Persist assistant response
                var finalContent = fullContent.ToString();
                db.ChatMessages.Add(new ChatMessage
                {
                    SessionId = sessionId,
                    Role      = MessageRole.Assistant,
                    Content   = finalContent
                });
                session.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);

                await hub.Clients.Group($"session-{sessionId}").SendAsync(
                    "AgentStatus",
                    new AgentStatusEvent(sessionId, assistantJobId, "Assistant", "Succeeded", "Response complete"),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message for session {SessionId}", sessionId);

                // Always send a final chunk so the frontend unblocks isSending
                var errorChunk = fullContent.Length > 0 ? string.Empty : "_(Error: agent failed to respond. Please try again.)_";
                await hub.Clients.Group($"session-{sessionId}").SendAsync(
                    "MessageChunk",
                    new MessageChunkEvent(sessionId, assistantMessageId, errorChunk, true),
                    CancellationToken.None);

                await hub.Clients.Group($"session-{sessionId}").SendAsync(
                    "AgentStatus",
                    new AgentStatusEvent(sessionId, assistantJobId, "Assistant", "Failed", ex.Message),
                    CancellationToken.None);
            }
        }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled assistant processing error for session {SessionId}", sessionId);

            await hub.Clients.Group($"session-{sessionId}").SendAsync(
                "AgentStatus",
                new AgentStatusEvent(sessionId, assistantJobId, "Assistant", "Failed", ex.Message),
                CancellationToken.None);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private static string BuildSystemMessage(string projectName, string orgName) =>
        $"""
        You are the Assistant agent for the Azure DevOps project '{projectName}' in org '{orgName}'.
        You have access to Azure DevOps tools via the ADO MCP server.

        Your responsibilities:
        1. Help the user understand which work items to prioritize. Use work item queries
           (sprint, tags, state) to surface the most actionable items.
        2. When the user asks you to start work on a specific item, delegate to the
           Developer sub-agent by outputting a delegate block like:
           DELEGATE_DEVELOP workItemId=<id> workItemTitle=<title> repoName=<repo>
        3. If the Developer agent reports it cannot complete a task, summarize the blocker
           clearly and suggest what the user needs to clarify or update in the work item.
        4. For reporting questions, acknowledge that the Analyst agent is coming soon.

        Always be concise and actionable.
        """;

    private static string ExtractOrgName(string orgUrl)
    {
        // https://dev.azure.com/contoso  →  contoso
        var uri = new Uri(orgUrl);
        return uri.Segments.LastOrDefault()?.TrimEnd('/') ?? uri.Host;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.ForceStopAsync();
            await _client.DisposeAsync();
        }
        if (_mcpConfigKey is not null)
            mcp.CleanupConfig(_mcpConfigKey);
    }
}
