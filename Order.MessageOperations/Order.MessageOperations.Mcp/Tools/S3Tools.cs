using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for S3 operations.
/// </summary>
[McpServerToolType]
public class S3Tools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public S3Tools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// List S3 buckets in LocalStack or AWS.
    /// </summary>
    [McpServerTool]
    [Description("List all S3 buckets in LocalStack (default) or AWS. Use to discover available storage.")]
    public async Task<string> ListS3Buckets(
        [Description("Target environment: 'localstack' (default) or 'aws'")] 
        string target = "localstack",
        CancellationToken ct = default)
    {
        target = NormalizeTarget(target);

        var buckets = await _client.ListS3BucketsAsync(target, ct);
        
        if (buckets.Count == 0)
        {
            return $"No S3 buckets found in {target}.";
        }

        var result = new
        {
            target,
            count = buckets.Count,
            buckets = buckets.Select(b => new
            {
                name = b.Name,
                createdAt = b.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")
            })
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// List objects in an S3 bucket.
    /// </summary>
    [McpServerTool]
    [Description("List objects in an S3 bucket. Supports prefix filtering and pagination.")]
    public async Task<string> ListS3Objects(
        [Description("The bucket name to list objects from")] 
        string bucketName,
        [Description("Optional prefix to filter objects (e.g., 'orders/2026/')")] 
        string? prefix = null,
        [Description("Maximum number of objects to return (default: 100, max: 1000)")] 
        int maxKeys = 100,
        [Description("Target environment: 'localstack' (default) or 'aws'")] 
        string target = "localstack",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return "Error: bucketName is required.";
        }

        target = NormalizeTarget(target);
        maxKeys = Math.Clamp(maxKeys, 1, 1000);

        var objects = await _client.ListS3ObjectsAsync(bucketName, prefix, maxKeys, target, ct);
        
        if (objects.Count == 0)
        {
            var msg = string.IsNullOrEmpty(prefix)
                ? $"No objects found in bucket '{bucketName}'."
                : $"No objects found in bucket '{bucketName}' with prefix '{prefix}'.";
            return msg;
        }

        var totalSize = objects.Sum(o => o.Size);

        var result = new
        {
            target,
            bucketName,
            prefix = prefix ?? "(none)",
            count = objects.Count,
            totalSizeBytes = totalSize,
            totalSizeFormatted = FormatSize(totalSize),
            objects = objects.Select(o => new
            {
                key = o.Key,
                size = FormatSize(o.Size),
                lastModified = o.LastModified.ToString("yyyy-MM-dd HH:mm:ss")
            })
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Get metadata for a specific S3 object.
    /// </summary>
    [McpServerTool]
    [Description("Get metadata for a specific S3 object including content type, size, and last modified date.")]
    public async Task<string> GetS3ObjectMetadata(
        [Description("The bucket name containing the object")] 
        string bucketName,
        [Description("The object key (path) to get metadata for")] 
        string key,
        [Description("Target environment: 'localstack' (default) or 'aws'")] 
        string target = "localstack",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return "Error: bucketName is required.";
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Error: key is required.";
        }

        target = NormalizeTarget(target);

        var metadata = await _client.GetS3ObjectMetadataAsync(bucketName, key, target, ct);
        
        if (metadata == null)
        {
            return $"Object not found: s3://{bucketName}/{key}";
        }

        var result = new
        {
            target,
            bucket = metadata.Bucket,
            key = metadata.Key,
            contentType = metadata.ContentType,
            size = FormatSize(metadata.ContentLength),
            sizeBytes = metadata.ContentLength,
            lastModified = metadata.LastModified.ToString("yyyy-MM-dd HH:mm:ss"),
            eTag = metadata.ETag
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Get the content of an S3 object (text/JSON files only).
    /// </summary>
    [McpServerTool]
    [Description("Get the content of an S3 object. Best for text/JSON files. Large files will be truncated.")]
    public async Task<string> GetS3ObjectContent(
        [Description("The bucket name containing the object")] 
        string bucketName,
        [Description("The object key (path) to read")] 
        string key,
        [Description("Maximum bytes to return (default: 256KB, max: 1MB)")] 
        int maxBytes = 262144,
        [Description("Target environment: 'localstack' (default) or 'aws'")] 
        string target = "localstack",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return "Error: bucketName is required.";
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Error: key is required.";
        }

        target = NormalizeTarget(target);
        maxBytes = Math.Clamp(maxBytes, 1024, 1048576);

        var content = await _client.GetS3ObjectContentAsync(bucketName, key, maxBytes, target, ct);
        
        if (content == null)
        {
            return $"Failed to read object: s3://{bucketName}/{key}";
        }

        var isTruncated = content.ContentLength > maxBytes;

        var result = new
        {
            target,
            bucket = content.Bucket,
            key = content.Key,
            contentType = content.ContentType,
            contentLength = content.ContentLength,
            truncated = isTruncated,
            content = content.Content
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Sync S3 objects referenced in batch messages to LocalStack.
    /// </summary>
    [McpServerTool]
    [Description("Sync S3 objects referenced in batch messages from AWS to LocalStack. Useful for replaying messages that reference S3 objects.")]
    public async Task<string> SyncS3FromBatch(
        [Description("The queue type folder name (e.g., 'incomingorders')")] 
        string queueType,
        [Description("The batch identifier containing messages with S3 references")] 
        string batchId,
        [Description("Whether to use AWS as fallback if objects not in LocalStack (default: true)")] 
        bool useAwsFallback = true,
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

        var request = new S3SyncRequest(
            QueueType: queueType,
            BatchId: batchId,
            UseAwsFallback: useAwsFallback
        );

        var result = await _client.SyncS3FromBatchAsync(request, ct);
        
        if (result == null)
        {
            return $"Failed to sync S3 objects for batch '{batchId}'.";
        }

        var response = new
        {
            success = true,
            synced = result.Synced,
            totalMessages = result.TotalMessages,
            useAwsFallback = result.UseAwsFallback,
            status = result.Synced > 0 
                ? $"Successfully synced {result.Synced} S3 objects to LocalStack."
                : "No S3 objects needed to be synced (or none found in messages)."
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    /// <summary>
    /// Upload an object to a LocalStack S3 bucket.
    /// </summary>
    [McpServerTool]
    [Description("Upload a text/JSON object to a LocalStack S3 bucket. Use this to place test order files in S3 to trigger S3 notifications and downstream processing.")]
    public async Task<string> UploadS3Object(
        [Description("The S3 bucket name (e.g., 'localstack-us-east-1-orders')")]
        string bucketName,
        [Description("The S3 object key/path (e.g., 'STANDARD/MERCHANT/SHIPMENT/order-123/abc123')")]
        string key,
        [Description("The content to upload (JSON or text)")]
        string content,
        [Description("The content type (default: 'application/json')")]
        string contentType = "application/json",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return "Error: bucketName is required.";
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Error: key is required.";
        }
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Error: content is required.";
        }

        var result = await _client.UploadS3ObjectAsync(bucketName, key, content, contentType, ct);

        if (result == null)
        {
            return $"Error: Failed to upload object to s3://{bucketName}/{key}.";
        }

        var response = new
        {
            success = true,
            bucket = result.BucketName,
            key = result.Key,
            eTag = result.ETag,
            summary = $"Uploaded object to s3://{result.BucketName}/{result.Key}"
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    private static string NormalizeTarget(string target)
    {
        return target?.ToLowerInvariant() switch
        {
            "aws" => "aws",
            _ => "localstack"
        };
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
