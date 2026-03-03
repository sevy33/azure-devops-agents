using Microsoft.AspNetCore.SignalR;
using AzureDevOpsAgents.Api.Models;

namespace AzureDevOpsAgents.Api.Hubs;

/// <summary>
/// Real-time hub for streaming agent responses and status updates to clients.
/// </summary>
public class AgentHub : Hub
{
    /// <summary>Subscribe a client to a specific chat session's updates.</summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");
    }

    /// <summary>Unsubscribe a client from a specific chat session.</summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sessionId}");
    }
}

/// <summary>Strongly-typed client contract for AgentHub.</summary>
public interface IAgentHubClient
{
    Task MessageChunk(MessageChunkEvent evt);
    Task AgentStatus(AgentStatusEvent evt);
    Task AgentLog(AgentLogEvent evt);
}
