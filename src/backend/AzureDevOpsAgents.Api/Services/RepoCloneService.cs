using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Data.Entities;
using AzureDevOpsAgents.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using AzureDevOpsAgents.Api.Hubs;

namespace AzureDevOpsAgents.Api.Services;

/// <summary>
/// Clones and manages local copies of Azure DevOps repositories so the
/// Developer agent (Copilot CLI) can operate on them.
/// </summary>
public class RepoCloneService(
    AppDbContext db,
    IHubContext<AgentHub> hubContext,
    IConfiguration configuration,
    TokenEncryptionService encryption,
    ILogger<RepoCloneService> logger)
{
    private readonly string _repoRoot = configuration["RepoStorage:Root"] ?? "/data/repos";

    public async Task<ProjectRepo> StartCloneAsync(Guid connectionId, string repoName, CancellationToken ct = default)
    {
        var connection = await db.Connections.FindAsync([connectionId], ct)
            ?? throw new KeyNotFoundException($"Connection {connectionId} not found.");

        var existing = await db.Repos
            .FirstOrDefaultAsync(r => r.ConnectionId == connectionId && r.RepoName == repoName, ct);

        if (existing is not null && existing.CloneStatus == CloneStatus.Ready)
            return existing;

        var localPath = Path.Combine(_repoRoot, SanitizeName(connection.OrganizationUrl), SanitizeName(connection.ProjectName), SanitizeName(repoName));

        ProjectRepo repo;
        if (existing is null)
        {
            repo = new ProjectRepo
            {
                ConnectionId = connectionId,
                RepoName = repoName,
                RemoteUrl = BuildRemoteUrl(connection, repoName),
                LocalClonePath = localPath,
                CloneStatus = CloneStatus.Cloning
            };
            db.Repos.Add(repo);
        }
        else
        {
            repo = existing;
            repo.CloneStatus = CloneStatus.Cloning;
            repo.CloneError = null;
        }

        await db.SaveChangesAsync(ct);

        // Clone in the background — caller gets the repo entity immediately
        _ = Task.Run(() => RunCloneAsync(repo.Id, connection, repoName, localPath), CancellationToken.None);

        return repo;
    }

    private async Task RunCloneAsync(Guid repoId, AzureDevOpsConnection connection, string repoName, string localPath)
    {
        try
        {
            Directory.CreateDirectory(localPath);

            var token = connection.AccessToken is not null
                ? encryption.Decrypt(connection.AccessToken)
                : throw new InvalidOperationException("No access token stored for this connection.");

            var org = new Uri(connection.OrganizationUrl).Host.Split('.')[0];
            var remoteUrl = $"https://{Uri.EscapeDataString("oauth2")}:{Uri.EscapeDataString(token)}@dev.azure.com/{org}/{Uri.EscapeDataString(connection.ProjectName)}/_git/{Uri.EscapeDataString(repoName)}";

            // If already cloned, do a fetch/reset instead
            bool isNewClone = !Directory.Exists(Path.Combine(localPath, ".git"));

            var args = isNewClone
                ? $"clone \"{remoteUrl}\" \"{localPath}\""
                : $"-C \"{localPath}\" fetch --all";

            var psi = new ProcessStartInfo("git", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();

            var repoEntry = await db.Repos.FindAsync(repoId);
            if (repoEntry is null) return;

            if (proc.ExitCode == 0)
            {
                repoEntry.CloneStatus = CloneStatus.Ready;
                repoEntry.ClonedAt = DateTime.UtcNow;
                logger.LogInformation("Repo {Repo} cloned to {Path}", repoName, localPath);
            }
            else
            {
                var error = await proc.StandardError.ReadToEndAsync();
                repoEntry.CloneStatus = CloneStatus.Failed;
                repoEntry.CloneError = error;
                logger.LogError("Clone failed for {Repo}: {Error}", repoName, error);
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error cloning {Repo}", repoName);
            var repoEntry = await db.Repos.FindAsync(repoId);
            if (repoEntry is not null)
            {
                repoEntry.CloneStatus = CloneStatus.Failed;
                repoEntry.CloneError = ex.Message;
                await db.SaveChangesAsync();
            }
        }
    }

    private static string BuildRemoteUrl(AzureDevOpsConnection conn, string repoName)
    {
        var org = new Uri(conn.OrganizationUrl).Segments.LastOrDefault()?.TrimEnd('/') ?? "unknown";
        return $"https://dev.azure.com/{org}/{Uri.EscapeDataString(conn.ProjectName)}/_git/{Uri.EscapeDataString(repoName)}";
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
}
