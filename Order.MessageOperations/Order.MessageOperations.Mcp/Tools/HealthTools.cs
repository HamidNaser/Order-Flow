using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for LocalStack health checks.
/// </summary>
[McpServerToolType]
public class HealthTools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HealthTools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Check LocalStack health by verifying SQS and S3 connectivity.
    /// </summary>
    [McpServerTool]
    [Description("Check LocalStack health. Verifies SQS and S3 connectivity and reports the status of each service.")]
    public async Task<string> CheckLocalStackHealth(CancellationToken ct = default)
    {
        var health = await _client.CheckLocalStackHealthAsync(ct);

        if (health == null)
        {
            return "Error: Unable to reach the MessageOperations API. Is it running?";
        }

        var result = new
        {
            healthy = health.Healthy,
            endpoint = health.LocalStackEndpoint,
            services = new
            {
                sqs = new { healthy = health.Sqs.Healthy, detail = health.Sqs.Detail },
                s3 = new { healthy = health.S3.Healthy, detail = health.S3.Detail }
            },
            summary = health.Healthy
                ? "LocalStack is healthy. All services are reachable."
                : "LocalStack is unhealthy. Check the service details above."
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
