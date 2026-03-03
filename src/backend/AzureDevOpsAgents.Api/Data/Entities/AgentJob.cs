namespace AzureDevOpsAgents.Api.Data.Entities;

public enum AgentType { Developer, Analyst }

public enum JobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public class AgentJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? RepoId { get; set; }
    public AgentType AgentType { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string? WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
    public string? Prompt { get; set; }
    public string? Log { get; set; }
    public string? ResultUrl { get; set; }   // PR URL on success
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ChatSession Session { get; set; } = null!;
    public ProjectRepo? Repo { get; set; }
}
