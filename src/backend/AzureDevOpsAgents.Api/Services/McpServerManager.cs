using System.Text.Json;

namespace AzureDevOpsAgents.Api.Services;

/// <summary>
/// Manages Azure DevOps MCP server configuration for Copilot CLI sessions.
/// Writes per-session mcp-config.json files that the CLI picks up via its
/// --mcp-config flag, passed through CopilotClientOptions.CliArgs.
/// </summary>
public class McpServerManager(IConfiguration configuration, ILogger<McpServerManager> logger)
{
    private readonly string _configRoot = configuration["McpServer:ConfigRoot"]
        ?? Path.Combine(Path.GetTempPath(), "ado-agents", "mcp-configs");

    /// <summary>
    /// Write an mcp-config.json file for the given session scope and return the
    /// absolute path so it can be passed to the CLI as --mcp-config &lt;path&gt;.
    /// </summary>
    public string WriteMcpConfig(string sessionId, string orgName, string accessToken, AgentRole role)
    {
        Directory.CreateDirectory(_configRoot);
        var filePath = Path.Combine(_configRoot, $"{sessionId}.json");

        var domains = role switch
        {
            AgentRole.Assistant => new[] { "core", "work", "work-items", "repositories" },
            AgentRole.Developer => new[] { "core", "work-items", "repositories" },
            AgentRole.Analyst   => new[] { "core", "work", "work-items", "pipelines" },
            _                   => new[] { "core" }
        };

        var args = new List<string> { "-y", "@azure-devops/mcp", orgName, "-d" };
        args.AddRange(domains);

        var config = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["ado"] = new
                {
                    command = "npx",
                    args = args.ToArray(),
                    env = new Dictionary<string, string>
                    {
                        ["AZURE_DEVOPS_TOKEN"] = accessToken,
                        ["AZURE_DEVOPS_ORG"]   = orgName
                    }
                }
            }
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        logger.LogDebug("Wrote MCP config for session {Session} to {Path}", sessionId, filePath);
        return filePath;
    }

    /// <summary>Remove the temporary config file when a session is torn down.</summary>
    public void CleanupConfig(string sessionId)
    {
        var filePath = Path.Combine(_configRoot, $"{sessionId}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            logger.LogDebug("Cleaned up MCP config for session {Session}", sessionId);
        }
    }
}

public enum AgentRole { Assistant, Developer, Analyst }
