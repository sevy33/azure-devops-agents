namespace AzureDevOpsAgents.Api.Data.Entities;

public enum CloneStatus
{
    Pending,
    Cloning,
    Ready,
    Failed
}

public class ProjectRepo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionId { get; set; }
    public required string RepoName { get; set; }
    public required string RemoteUrl { get; set; }
    public string? LocalClonePath { get; set; }
    public CloneStatus CloneStatus { get; set; } = CloneStatus.Pending;
    public string? CloneError { get; set; }
    public DateTime? ClonedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AzureDevOpsConnection Connection { get; set; } = null!;
    public ICollection<AgentJob> AgentJobs { get; set; } = [];
}
