using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Models;
using AzureDevOpsAgents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AzureDevOpsAgents.Api.Controllers;

/// <summary>
/// Lists ADO repositories for a connection and manages server-side cloning.
/// </summary>
[ApiController]
[Route("api/connections/{connectionId:guid}/repos")]
public class ReposController(
    AppDbContext db,
    RepoCloneService cloneService,
    TokenEncryptionService encryption,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    /// <summary>
    /// List repos for the connection — fetches from ADO REST API and merges
    /// with local DB clone status.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRepos(Guid connectionId, CancellationToken ct)
    {
        var connection = await db.Connections.FindAsync([connectionId], ct);
        if (connection is null) return NotFound();
        if (connection.AccessToken is null) return BadRequest("Connection not authenticated.");

        var accessToken = encryption.Decrypt(connection.AccessToken);

        // Fetch repos from ADO REST API
        var org     = ExtractOrgName(connection.OrganizationUrl);
        var project = Uri.EscapeDataString(connection.ProjectName);
        var apiUrl  = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories?api-version=7.1";

        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.GetAsync(apiUrl, ct);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch repositories from Azure DevOps.");

        var json     = await response.Content.ReadAsStringAsync(ct);
        var adoRepos = JsonDocument.Parse(json).RootElement.GetProperty("value");

        // Load local clone statuses
        var localRepos = await db.Repos
            .Where(r => r.ConnectionId == connectionId)
            .ToDictionaryAsync(r => r.RepoName, ct);

        var result = adoRepos.EnumerateArray().Select(repo =>
        {
            var name = repo.GetProperty("name").GetString()!;
            localRepos.TryGetValue(name, out var local);
            return new RepoDto(
                local?.Id ?? Guid.Empty,
                name,
                repo.GetProperty("remoteUrl").GetString()!,
                local?.CloneStatus.ToString() ?? "NotCloned",
                local?.ClonedAt);
        }).ToList();

        return Ok(result);
    }

    /// <summary>Start an async clone of the specified repo.</summary>
    [HttpPost("{repoName}/clone")]
    public async Task<IActionResult> Clone(Guid connectionId, string repoName, CancellationToken ct)
    {
        var repo = await cloneService.StartCloneAsync(connectionId, repoName, ct);
        return Ok(new RepoDto(repo.Id, repo.RepoName, repo.RemoteUrl, repo.CloneStatus.ToString(), repo.ClonedAt));
    }

    /// <summary>Get current clone status of a repo.</summary>
    [HttpGet("{repoName}/status")]
    public async Task<IActionResult> Status(Guid connectionId, string repoName, CancellationToken ct)
    {
        var repo = await db.Repos
            .FirstOrDefaultAsync(r => r.ConnectionId == connectionId && r.RepoName == repoName, ct);

        if (repo is null)
            return Ok(new { status = "NotCloned", error = (string?)null });

        return Ok(new { status = repo.CloneStatus.ToString(), error = repo.CloneError });
    }

    private static string ExtractOrgName(string orgUrl)
    {
        var uri = new Uri(orgUrl);
        return uri.Segments.LastOrDefault()?.TrimEnd('/') ?? uri.Host;
    }
}
