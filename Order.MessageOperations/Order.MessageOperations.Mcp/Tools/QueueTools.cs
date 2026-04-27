using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for queue operations.
/// </summary>
[McpServerToolType]
public class QueueTools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public QueueTools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// List all configured queue mappings from the API configuration.
    /// </summary>
    [McpServerTool]
    [Description("List all configured queue mappings. Shows available queue keys with their LocalStack and AWS queue names.")]
    public async Task<string> ListConfiguredQueues(CancellationToken ct = default)
    {
        var queues = await _client.ListConfiguredQueuesAsync(ct);
        
        if (queues.Count == 0)
        {
            return "No queue mappings configured.";
        }

        var result = new
        {
            count = queues.Count,
            queues = queues.Select(q => new
            {
                key = q.QueueKey,
                displayName = q.DisplayName,
                localStack = q.LocalStackQueueName,
                awsDlq = q.AwsDlqName,
                enabled = q.Enabled
            })
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// List all queues currently existing in LocalStack or AWS.
    /// </summary>
    [McpServerTool]
    [Description("List all SQS queues in LocalStack or AWS. Use target='localstack' (default) or target='aws' to choose the environment.")]
    public async Task<string> ListLocalStackQueues(
        [Description("Target environment: 'localstack' (default) or 'aws'")] string target = "localstack",
        CancellationToken ct = default)
    {
        var queues = await _client.ListQueuesAsync(target, ct);
        
        if (queues.Count == 0)
        {
            return "No queues found in LocalStack.";
        }

        var queueNames = queues.Select(url =>
        {
            var parts = url.Split('/');
            return parts.Length > 0 ? parts[^1] : url;
        }).ToList();

        var result = new
        {
            count = queues.Count,
            queues = queueNames,
            fullUrls = queues
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Get status and attributes for a specific LocalStack queue.
    /// </summary>
    [McpServerTool]
    [Description("Get status and attributes for a specific queue. Shows message counts and queue metadata. Use target='localstack' (default) or target='aws'.")]
    public async Task<string> GetQueueStatus(
        [Description("The name of the queue to check (e.g., 'order-gateway-incoming')")] 
        string queueName,
        [Description("Target environment: 'localstack' (default) or 'aws'")] string target = "localstack",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            return "Error: queueName is required.";
        }

        var attributes = await _client.GetQueueStatusAsync(queueName, target, ct);
        
        if (attributes.Count == 0)
        {
            return $"No attributes found for queue '{queueName}'. The queue may not exist.";
        }

        var approxMessages = attributes.GetValueOrDefault("ApproximateNumberOfMessages", "0");
        var approxNotVisible = attributes.GetValueOrDefault("ApproximateNumberOfMessagesNotVisible", "0");
        var approxDelayed = attributes.GetValueOrDefault("ApproximateNumberOfMessagesDelayed", "0");

        var result = new
        {
            queueName,
            summary = new
            {
                messagesReady = int.TryParse(approxMessages, out var m) ? m : 0,
                messagesInFlight = int.TryParse(approxNotVisible, out var nv) ? nv : 0,
                messagesDelayed = int.TryParse(approxDelayed, out var d) ? d : 0
            },
            allAttributes = attributes
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Peek at messages in a LocalStack queue without consuming them.
    /// </summary>
    [McpServerTool]
    [Description("Peek at messages in a queue without consuming them. Returns message IDs and bodies. Use target='localstack' (default) or target='aws'.")]
    public async Task<string> PeekQueueMessages(
        [Description("The name of the queue to peek (e.g., 'order-gateway-incoming')")] 
        string queueName,
        [Description("Number of messages to peek (1-10, default: 5)")] 
        int count = 5,
        [Description("Target environment: 'localstack' (default) or 'aws'")] string target = "localstack",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            return "Error: queueName is required.";
        }

        count = Math.Clamp(count, 1, 10);

        var messages = await _client.PeekQueueMessagesAsync(queueName, count, target, ct);
        
        if (messages.Count == 0)
        {
            return $"No messages found in queue '{queueName}'.";
        }

        var result = new
        {
            queueName,
            retrieved = messages.Count,
            messages = messages.Select((m, i) => new
            {
                index = i + 1,
                messageId = m.MessageId,
                bodySize = m.BodySize,
                body = TruncateBody(m.Body, 1000)
            })
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private static string TruncateBody(string body, int maxLength)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= maxLength)
        {
            return body;
        }
        return body[..maxLength] + "... [truncated]";
    }
}
