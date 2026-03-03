namespace AzureDevOpsAgents.Api.Services;

/// <summary>
/// Analyst sub-agent — stub for future reporting capabilities.
/// </summary>
public class AnalystAgentService(ILogger<AnalystAgentService> logger)
{
    public Task<string> GetStatusAsync() =>
        Task.FromResult("The Analyst agent is coming soon. It will provide reporting on work items and pull requests.");
}
