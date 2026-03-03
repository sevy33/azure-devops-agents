using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Data.Entities;
using AzureDevOpsAgents.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AzureDevOpsAgents.Api.Controllers;

/// <summary>
/// Manages Azure DevOps org/project connections.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConnectionsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var connections = await db.Connections
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ConnectionDto(c.Id, c.DisplayName, c.OrganizationUrl, c.ProjectName, c.CreatedAt, !string.IsNullOrEmpty(c.AccessToken)))
            .ToListAsync(ct);

        return Ok(connections);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var c = await db.Connections.FindAsync([id], ct);
        if (c is null) return NotFound();
        return Ok(new ConnectionDto(c.Id, c.DisplayName, c.OrganizationUrl, c.ProjectName, c.CreatedAt, !string.IsNullOrEmpty(c.AccessToken)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConnectionRequest req, CancellationToken ct)
    {
        var conn = new AzureDevOpsConnection
        {
            DisplayName     = req.DisplayName,
            OrganizationUrl = req.OrganizationUrl.TrimEnd('/'),
            ProjectName     = req.ProjectName
        };
        db.Connections.Add(conn);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = conn.Id },
            new ConnectionDto(conn.Id, conn.DisplayName, conn.OrganizationUrl, conn.ProjectName, conn.CreatedAt, false));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var c = await db.Connections.FindAsync([id], ct);
        if (c is null) return NotFound();
        db.Connections.Remove(c);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
