namespace AzureDevOpsAgents.Api.Models;

// ── Connections ──────────────────────────────────────────────────────────────

public record CreateConnectionRequest(
    string DisplayName,
    string OrganizationUrl,
    string ProjectName);

public record ConnectionDto(
    Guid Id,
    string DisplayName,
    string OrganizationUrl,
    string ProjectName,
    DateTime CreatedAt,
    bool IsAuthenticated);

// ── Repos ────────────────────────────────────────────────────────────────────

public record RepoDto(
    Guid Id,
    string RepoName,
    string RemoteUrl,
    string CloneStatus,
    DateTime? ClonedAt);

// ── Chat ─────────────────────────────────────────────────────────────────────

public record CreateSessionRequest(Guid ConnectionId, string? Title);

public record SessionDto(
    Guid Id,
    Guid ConnectionId,
    string? Title,
    DateTime CreatedAt);

public record SendMessageRequest(string Content);

public record ChatMessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt);

// ── Agent Jobs ───────────────────────────────────────────────────────────────

public record AgentJobDto(
    Guid Id,
    string AgentType,
    string Status,
    string? WorkItemId,
    string? WorkItemTitle,
    string? ResultUrl,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ── SignalR events ────────────────────────────────────────────────────────────

public record MessageChunkEvent(Guid SessionId, Guid MessageId, string Chunk, bool IsFinal);

public record AgentStatusEvent(Guid SessionId, Guid JobId, string AgentType, string Status, string? Detail);

public record AgentLogEvent(Guid JobId, string Line);
