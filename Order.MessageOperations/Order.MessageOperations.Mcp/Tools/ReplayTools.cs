using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for replay operations.
/// </summary>
[McpServerToolType]
public class ReplayTools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ReplayTools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Download messages from an AWS DLQ to a local batch.
    /// </summary>
    [McpServerTool]
    [Description("Download messages from an AWS queue (typically a DLQ) and save them to a local batch for later replay or inspection.")]
    public async Task<string> DownloadMessages(
        [Description("The queue key from configuration (e.g., 'IncomingOrders', 'IngestStandard')")]  
        string queueKey,
        [Description("Maximum number of messages to download (default: 100, max: 1000)")] 
        int maxMessages = 100,
        [Description("Optional: specific message ID to download (if known)")] 
        string? messageId = null,
        [Description("Optional: override the AWS queue name (defaults to configured DLQ)")] 
        string? awsQueueName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueKey))
        {
            return "Error: queueKey is required. Use 'ListConfiguredQueues' to see available queue keys.";
        }

        maxMessages = Math.Clamp(maxMessages, 1, 1000);

        var request = new DownloadRequest(
            QueueKey: queueKey,
            AwsQueueName: awsQueueName,
            MaxMessages: maxMessages,
            MessageId: messageId
        );

        var result = await _client.DownloadMessagesAsync(request, ct);
        
        if (result == null)
        {
            return $"Failed to download messages for queue key '{queueKey}'.";
        }

        var response = new
        {
            success = true,
            downloaded = result.Downloaded,
            queueKey = result.QueueKey,
            awsQueueName = result.AwsQueueName,
            batchPath = result.BatchPath,
            nextSteps = result.Downloaded > 0 
                ? "Use 'ListBatches' to see the saved batch, then 'ReplayFromBatch' to replay to LocalStack."
                : "No messages were downloaded. The queue may be empty."
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    /// <summary>
    /// Replay a saved batch to a LocalStack queue.
    /// </summary>
    [McpServerTool]
    [Description("Replay messages from a saved batch to a LocalStack queue. Use 'ListBatches' first to find available batches.")]
    public async Task<string> ReplayFromBatch(
        [Description("The queue type folder name (e.g., 'incomingorders')")] 
        string queueType,
        [Description("The batch identifier (e.g., '2026-04-15_143022_batch-abc123')")] 
        string batchId,
        [Description("Optional: override the LocalStack queue name (defaults to configured queue)")] 
        string? localStackQueueName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueType))
        {
            return "Error: queueType is required. Use 'ListBatches' to see available queue types.";
        }
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return "Error: batchId is required. Use 'ListBatches' to see available batch IDs.";
        }

        var request = new ReplayFromBatchRequest(
            QueueType: queueType,
            BatchId: batchId,
            LocalStackQueueName: localStackQueueName
        );

        var result = await _client.ReplayFromBatchAsync(request, ct);
        
        if (result == null)
        {
            return $"Failed to replay batch '{batchId}' from queue type '{queueType}'.";
        }

        var response = new
        {
            success = true,
            replayed = result.Replayed,
            total = result.Total,
            localStackQueueName = result.LocalStackQueueName,
            status = result.Replayed == result.Total 
                ? "All messages replayed successfully."
                : $"Warning: Only {result.Replayed} of {result.Total} messages were replayed."
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    /// <summary>
    /// Download from AWS and immediately replay to LocalStack in one operation.
    /// </summary>
    [McpServerTool]
    [Description("Download messages from an AWS queue and immediately replay them to LocalStack. Combines download and replay into one step.")]
    public async Task<string> DownloadAndReplay(
        [Description("The queue key from configuration (e.g., 'IncomingOrders', 'IngestStandard')")]  
        string queueKey,
        [Description("Maximum number of messages to download and replay (default: 100)")] 
        int maxMessages = 100,
        [Description("Optional: specific message ID to download and replay (if known)")] 
        string? messageId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueKey))
        {
            return "Error: queueKey is required. Use 'ListConfiguredQueues' to see available queue keys.";
        }

        maxMessages = Math.Clamp(maxMessages, 1, 1000);

        var request = new DownloadAndReplayRequest(
            QueueKey: queueKey,
            MaxMessages: maxMessages,
            MessageId: messageId
        );

        var result = await _client.DownloadAndReplayAsync(request, ct);
        
        if (result == null)
        {
            return $"Failed to download and replay messages for queue key '{queueKey}'.";
        }

        var response = new
        {
            success = true,
            queueKey = result.QueueKey,
            replayed = result.Replayed,
            status = result.Replayed > 0 
                ? $"Successfully downloaded and replayed {result.Replayed} messages to LocalStack."
                : "No messages were found to replay. The AWS queue may be empty."
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }
}
