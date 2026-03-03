using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Data.Entities;
using AzureDevOpsAgents.Api.Models;
using AzureDevOpsAgents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AzureDevOpsAgents.Api.Controllers;

/// <summary>
/// Chat session management and message sending.
/// </summary>
[ApiController]
[Route("api/chat")]
public class ChatController(
    AppDbContext db,
    AssistantAgentService assistant) : ControllerBase
{
    // ── Sessions ─────────────────────────────────────────────────────────────

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] Guid connectionId, CancellationToken ct)
    {
        var sessions = await db.ChatSessions
            .Where(s => s.ConnectionId == connectionId)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SessionDto(s.Id, s.ConnectionId, s.Title, s.CreatedAt))
            .ToListAsync(ct);
        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req, CancellationToken ct)
    {
        var conn = await db.Connections.FindAsync([req.ConnectionId], ct);
        if (conn is null) return NotFound("Connection not found.");

        var session = new ChatSession
        {
            ConnectionId = req.ConnectionId,
            Title        = req.Title ?? $"Chat {DateTime.UtcNow:g}"
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetSession), new { sessionId = session.Id },
            new SessionDto(session.Id, session.ConnectionId, session.Title, session.CreatedAt));
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken ct)
    {
        var session = await db.ChatSessions.FindAsync([sessionId], ct);
        if (session is null) return NotFound();
        return Ok(new SessionDto(session.Id, session.ConnectionId, session.Title, session.CreatedAt));
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid sessionId, CancellationToken ct)
    {
        var messages = await db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto(m.Id, m.Role.ToString(), m.Content, m.CreatedAt))
            .ToListAsync(ct);
        return Ok(messages);
    }

    // ── Messaging ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Send a message to the Assistant agent. The response streams via SignalR;
    /// this endpoint returns 202 Accepted immediately.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid sessionId,
        [FromBody] SendMessageRequest req,
        CancellationToken ct)
    {
        var session = await db.ChatSessions.FindAsync([sessionId], ct);
        if (session is null) return NotFound();

        // Process asynchronously — response streams via SignalR
        _ = Task.Run(() => assistant.ProcessMessageAsync(sessionId, req.Content), CancellationToken.None);

        return Accepted();
    }

    // ── Agent Jobs ────────────────────────────────────────────────────────────

    [HttpGet("sessions/{sessionId:guid}/jobs")]
    public async Task<IActionResult> GetJobs(Guid sessionId, CancellationToken ct)
    {
        var jobs = await db.AgentJobs
            .Where(j => j.SessionId == sessionId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new AgentJobDto(
                j.Id, j.AgentType.ToString(), j.Status.ToString(),
                j.WorkItemId, j.WorkItemTitle, j.ResultUrl, j.ErrorMessage,
                j.CreatedAt, j.UpdatedAt))
            .ToListAsync(ct);
        return Ok(jobs);
    }
}
