using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for tracing message flow through LocalStack queues, S3, and MongoDB.
/// These polling-based tools let the AI agent verify that injected messages arrive
/// at downstream systems within a configurable timeout.
/// </summary>
[McpServerToolType]
public class TraceTools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TraceTools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Poll LocalStack S3 until an object matching the key prefix appears, or timeout.
    /// Use this after sending a message to verify that the downstream worker wrote the expected S3 object.
    /// </summary>
    [McpServerTool]
    [Description("Wait for an S3 object to appear in a LocalStack bucket. Polls until an object matching the key prefix is found or the timeout expires. Use after sending a message to verify downstream S3 writes.")]
    public async Task<string> WaitForS3Object(
        [Description("The S3 bucket name to poll")] string bucketName,
        [Description("The S3 key prefix to match (e.g. 'orders/store-123/')")] string keyPrefix,
        [Description("Maximum seconds to wait (default: 30)")] int timeoutSeconds = 30,
        [Description("Milliseconds between polls (default: 500)")] int pollIntervalMs = 500,
        CancellationToken ct = default)
    {
        var result = await _client.WaitForS3ObjectAsync(bucketName, keyPrefix, timeoutSeconds, pollIntervalMs, ct);

        if (result == null)
            return "Error: Unable to reach the MessageOperations API. Is it running?";

        return JsonSerializer.Serialize(new
        {
            result.Found,
            result.BucketName,
            result.KeyPrefix,
            result.ElapsedMs,
            result.TimeoutMs,
            result.MatchedKey,
            result.Size,
            result.Detail,
            summary = result.Found
                ? $"Found S3 object '{result.MatchedKey}' ({result.Size} bytes) after {result.ElapsedMs}ms"
                : $"No S3 object with prefix '{result.KeyPrefix}' found within {result.TimeoutMs}ms"
        }, JsonOptions);
    }

    /// <summary>
    /// Poll a LocalStack SQS queue until a message (optionally matching body text) appears, or timeout.
    /// Use this to verify that a message was forwarded to a downstream queue.
    /// </summary>
    [McpServerTool]
    [Description("Wait for a message to appear in a LocalStack SQS queue. Optionally filter by body content. Polls until found or timeout. Use to verify message routing between queues.")]
    public async Task<string> WaitForQueueMessage(
        [Description("The SQS queue name to poll")] string queueName,
        [Description("Optional text that must appear in the message body")] string? bodyContains = null,
        [Description("Maximum seconds to wait (default: 30)")] int timeoutSeconds = 30,
        [Description("Milliseconds between polls (default: 500)")] int pollIntervalMs = 500,
        CancellationToken ct = default)
    {
        var result = await _client.WaitForQueueMessageAsync(queueName, bodyContains, timeoutSeconds, pollIntervalMs, ct);

        if (result == null)
            return "Error: Unable to reach the MessageOperations API. Is it running?";

        return JsonSerializer.Serialize(new
        {
            result.Found,
            result.QueueName,
            result.BodyContains,
            result.ElapsedMs,
            result.TimeoutMs,
            result.MessageId,
            result.BodyPreview,
            result.Detail,
            summary = result.Found
                ? $"Found message '{result.MessageId}' in queue '{result.QueueName}' after {result.ElapsedMs}ms"
                : $"No matching message found in queue '{result.QueueName}' within {result.TimeoutMs}ms"
        }, JsonOptions);
    }

    /// <summary>
    /// Poll MongoDB until a matching document appears for the given store, or timeout.
    /// Use this to verify that an order message was processed and persisted to the database.
    /// </summary>
    [McpServerTool]
    [Description("Wait for a MongoDB document to appear for a given store. Search by providerOrderId or customerId. Use to verify that order processing completed and wrote to the database.")]
    public async Task<string> WaitForMongoDocument(
        [Description("The StoreId (CoOrg) to search in")] string storeId,
        [Description("Optional provider order ID to match")] string? providerOrderId = null,
        [Description("Optional customer ID to filter by")] string? customerId = null,
        [Description("Maximum seconds to wait (default: 30)")] int timeoutSeconds = 30,
        [Description("Milliseconds between polls (default: 500)")] int pollIntervalMs = 500,
        CancellationToken ct = default)
    {
        var result = await _client.WaitForMongoDocumentAsync(storeId, providerOrderId, customerId, timeoutSeconds, pollIntervalMs, ct);

        if (result == null)
            return "Error: Unable to reach the MessageOperations API. Is it running?";

        return JsonSerializer.Serialize(new
        {
            result.Found,
            result.StoreId,
            result.ProviderOrderId,
            result.CustomerId,
            result.ElapsedMs,
            result.TimeoutMs,
            result.MatchedOrderId,
            result.Detail,
            summary = result.Found
                ? $"Found document '{result.MatchedOrderId}' for store '{result.StoreId}' after {result.ElapsedMs}ms"
                : $"No matching document found for store '{result.StoreId}' within {result.TimeoutMs}ms"
        }, JsonOptions);
    }

    /// <summary>
    /// Get the approximate message count for all configured LocalStack queues.
    /// Use this for a quick snapshot of queue depths before/after sending messages.
    /// </summary>
    [McpServerTool]
    [Description("Get the approximate message count for all configured LocalStack queues. Returns queue depths in a single call. Use to monitor queue activity before and after sending messages.")]
    public async Task<string> GetAllQueueDepths(CancellationToken ct = default)
    {
        var result = await _client.GetAllQueueDepthsAsync(ct);

        if (result == null)
            return "Error: Unable to reach the MessageOperations API. Is it running?";

        return JsonSerializer.Serialize(new
        {
            result.TotalMessages,
            queues = result.Queues.Select(q => new
            {
                q.QueueKey,
                q.QueueName,
                q.ApproximateMessageCount,
                q.ApproximateNotVisible
            }),
            summary = $"{result.Queues.Count} queues monitored, {result.TotalMessages} total messages"
        }, JsonOptions);
    }
}
