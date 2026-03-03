using System.Text.Json;

namespace AzureDevOpsAgents.Api.Data.Entities;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AzureDevOpsConnection Connection { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = [];
    public ICollection<AgentJob> AgentJobs { get; set; } = [];
}

public enum MessageRole { User, Assistant, System }

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public MessageRole Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ChatSession Session { get; set; } = null!;
}
