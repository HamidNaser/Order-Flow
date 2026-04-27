namespace Order.MessageOperations.Mcp.Configuration;

/// <summary>
/// Configuration options for the MCP server.
/// </summary>
public class McpServerOptions
{
    public const string SectionName = "McpServer";

    /// <summary>
    /// Base URL of the MessageOperations API.
    /// Default: http://localhost:5100
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5100";

    /// <summary>
    /// HTTP request timeout in seconds.
    /// Default: 30 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for failed requests.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds.
    /// Default: 1000ms
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}
