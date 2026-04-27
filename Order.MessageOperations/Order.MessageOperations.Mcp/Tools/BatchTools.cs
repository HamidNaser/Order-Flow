using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for batch operations.
/// </summary>
[McpServerToolType]
public class BatchTools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BatchTools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// List all saved message batches on disk.
    /// </summary>
    [McpServerTool]
    [Description("List all saved message batches organized by queue type. Shows batch IDs that can be used for replay or inspection.")]
    public async Task<string> ListBatches(CancellationToken ct = default)
    {
        var batches = await _client.ListBatchesAsync(ct);
        
        if (batches.Count == 0)
        {
            return "No saved batches found. Use 'DownloadMessages' to download messages from AWS to create a batch.";
        }

        var totalBatches = batches.Sum(b => b.BatchIds.Count);

        var result = new
        {
            totalBatches,
            queueTypes = batches.Count,
            batches = batches.Select(b => new
            {
                queueType = b.QueueType,
                batchCount = b.BatchIds.Count,
                batchIds = b.BatchIds
            })
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Get the manifest/details for a specific saved batch.
    /// </summary>
    [McpServerTool]
    [Description("Get details about a specific saved batch including message count, creation date, and source DLQ.")]
    public async Task<string> GetBatchDetails(
        [Description("The queue type folder name (e.g., 'incomingorders')")] 
        string queueType,
        [Description("The batch identifier (e.g., '2026-04-15_143022_batch-abc123')")] 
        string batchId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueType))
        {
            return "Error: queueType is required.";
        }
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return "Error: batchId is required.";
        }

        var manifest = await _client.GetBatchDetailsAsync(queueType, batchId, ct);
        
        if (manifest == null)
        {
            return $"Batch '{batchId}' not found in queue type '{queueType}'.";
        }

        var result = new
        {
            manifest.BatchId,
            manifest.QueueType,
            createdAt = manifest.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            sourceDlq = manifest.SourceDlq,
            messageCount = manifest.MessageCount,
            messageIds = manifest.MessageIds.Take(10).ToList(),
            hasMore = manifest.MessageIds.Count > 10 ? $"... and {manifest.MessageIds.Count - 10} more" : null
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Load and display messages from a saved batch.
    /// </summary>
    [McpServerTool]
    [Description("Load and display messages from a saved batch. Use to inspect message content before replay.")]
    public async Task<string> GetBatchMessages(
        [Description("The queue type folder name (e.g., 'incomingorders')")] 
        string queueType,
        [Description("The batch identifier (e.g., '2026-04-15_143022_batch-abc123')")] 
        string batchId,
        [Description("Maximum number of messages to return (default: 10, max: 50)")] 
        int maxMessages = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueType))
        {
            return "Error: queueType is required.";
        }
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return "Error: batchId is required.";
        }

        maxMessages = Math.Clamp(maxMessages, 1, 50);

        var messages = await _client.GetBatchMessagesAsync(queueType, batchId, ct);
        
        if (messages.Count == 0)
        {
            return $"No messages found in batch '{batchId}'.";
        }

        var displayMessages = messages.Take(maxMessages).ToList();

        var result = new
        {
            queueType,
            batchId,
            totalMessages = messages.Count,
            showing = displayMessages.Count,
            messages = displayMessages.Select((m, i) => new
            {
                index = i + 1,
                messageId = m.MessageId,
                downloadedAt = m.DownloadedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                bodyPreview = TruncateBody(m.Body, 500),
                hasGroupId = !string.IsNullOrEmpty(m.MessageGroupId)
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
