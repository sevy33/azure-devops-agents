namespace AzureDevOpsAgents.Api.Data.Entities;

public class AzureDevOpsConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DisplayName { get; set; }
    public required string OrganizationUrl { get; set; }   // e.g. https://dev.azure.com/contoso
    public required string ProjectName { get; set; }
    public string? AccessToken { get; set; }               // encrypted OAuth access token
    public string? RefreshToken { get; set; }              // encrypted OAuth refresh token
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ProjectRepo> Repos { get; set; } = [];
    public ICollection<ChatSession> ChatSessions { get; set; } = [];
}
