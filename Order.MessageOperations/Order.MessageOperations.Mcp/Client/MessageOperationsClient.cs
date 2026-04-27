using System.Net.Http.Json;
using System.Text.Json;
using Order.MessageOperations.Mcp.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Mcp.Client;

/// <summary>
/// Typed HTTP client for interacting with the MessageOperations API.
/// </summary>
public class MessageOperationsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MessageOperationsClient> _logger;
    private readonly McpServerOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MessageOperationsClient(
        HttpClient httpClient,
        IOptions<McpServerOptions> options,
        ILogger<MessageOperationsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    #region Queue Operations

    /// <summary>
    /// List all configured queue mappings.
    /// </summary>
    public async Task<List<QueueMappingDto>> ListConfiguredQueuesAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<QueueMappingDto>>("/api/v1/queues", ct) ?? [];
    }

    /// <summary>
    /// List all queues in LocalStack or AWS.
    /// </summary>
    public async Task<List<string>> ListQueuesAsync(string target = "localstack", CancellationToken ct = default)
    {
        return await GetAsync<List<string>>($"/api/v1/queues/list?target={Uri.EscapeDataString(target)}", ct) ?? [];
    }

    /// <summary>
    /// List all queues in LocalStack. Preserved for backward compatibility.
    /// </summary>
    public async Task<List<string>> ListLocalStackQueuesAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<string>>("/api/v1/queues/localstack", ct) ?? [];
    }

    /// <summary>
    /// Get status/attributes for a specific queue.
    /// </summary>
    public async Task<Dictionary<string, string>> GetQueueStatusAsync(string queueName, string target = "localstack", CancellationToken ct = default)
    {
        return await GetAsync<Dictionary<string, string>>($"/api/v1/queues/{Uri.EscapeDataString(queueName)}/status?target={Uri.EscapeDataString(target)}", ct) ?? new();
    }

    /// <summary>
    /// Peek at messages in a queue without consuming them.
    /// </summary>
    public async Task<List<PeekedMessageDto>> PeekQueueMessagesAsync(string queueName, int count = 5, string target = "localstack", CancellationToken ct = default)
    {
        return await GetAsync<List<PeekedMessageDto>>($"/api/v1/queues/{Uri.EscapeDataString(queueName)}/messages?count={count}&target={Uri.EscapeDataString(target)}", ct) ?? [];
    }

    #endregion

    #region Batch Operations

    /// <summary>
    /// List all saved message batches.
    /// </summary>
    public async Task<List<BatchGroupDto>> ListBatchesAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<BatchGroupDto>>("/api/v1/batches", ct) ?? [];
    }

    /// <summary>
    /// Get the manifest for a specific batch.
    /// </summary>
    public async Task<BatchManifestDto?> GetBatchDetailsAsync(string queueType, string batchId, CancellationToken ct = default)
    {
        return await GetAsync<BatchManifestDto>($"/api/v1/batches/{Uri.EscapeDataString(queueType)}/{Uri.EscapeDataString(batchId)}", ct);
    }

    /// <summary>
    /// Load all messages from a saved batch.
    /// </summary>
    public async Task<List<SavedMessageDto>> GetBatchMessagesAsync(string queueType, string batchId, CancellationToken ct = default)
    {
        return await GetAsync<List<SavedMessageDto>>($"/api/v1/batches/{Uri.EscapeDataString(queueType)}/{Uri.EscapeDataString(batchId)}/messages", ct) ?? [];
    }

    #endregion

    #region Replay Operations

    /// <summary>
    /// Download messages from an AWS queue to a local batch.
    /// </summary>
    public async Task<DownloadResultDto?> DownloadMessagesAsync(DownloadRequest request, CancellationToken ct = default)
    {
        return await PostAsync<DownloadResultDto>("/api/v1/replay/download", request, ct);
    }

    /// <summary>
    /// Replay a saved batch to a LocalStack queue.
    /// </summary>
    public async Task<ReplayResultDto?> ReplayFromBatchAsync(ReplayFromBatchRequest request, CancellationToken ct = default)
    {
        return await PostAsync<ReplayResultDto>("/api/v1/replay/from-batch", request, ct);
    }

    /// <summary>
    /// Download from AWS and immediately replay to LocalStack.
    /// </summary>
    public async Task<DownloadAndReplayResultDto?> DownloadAndReplayAsync(DownloadAndReplayRequest request, CancellationToken ct = default)
    {
        return await PostAsync<DownloadAndReplayResultDto>("/api/v1/replay/download-and-replay", request, ct);
    }

    #endregion

    #region S3 Operations

    /// <summary>
    /// List S3 buckets.
    /// </summary>
    public async Task<List<S3BucketDto>> ListS3BucketsAsync(string target = "localstack", CancellationToken ct = default)
    {
        return await GetAsync<List<S3BucketDto>>($"/api/v1/s3/buckets?target={target}", ct) ?? [];
    }

    /// <summary>
    /// List objects in an S3 bucket.
    /// </summary>
    public async Task<List<S3ObjectDto>> ListS3ObjectsAsync(string bucketName, string? prefix = null, int maxKeys = 100, string target = "localstack", CancellationToken ct = default)
    {
        var url = $"/api/v1/s3/buckets/{Uri.EscapeDataString(bucketName)}/objects?maxKeys={maxKeys}&target={target}";
        if (!string.IsNullOrEmpty(prefix))
        {
            url += $"&prefix={Uri.EscapeDataString(prefix)}";
        }
        return await GetAsync<List<S3ObjectDto>>(url, ct) ?? [];
    }

    /// <summary>
    /// Get metadata for an S3 object.
    /// </summary>
    public async Task<S3ObjectMetadataDto?> GetS3ObjectMetadataAsync(string bucketName, string key, string target = "localstack", CancellationToken ct = default)
    {
        return await GetAsync<S3ObjectMetadataDto>($"/api/v1/s3/buckets/{Uri.EscapeDataString(bucketName)}/objects/metadata?key={Uri.EscapeDataString(key)}&target={target}", ct);
    }

    /// <summary>
    /// Get the content of an S3 object.
    /// </summary>
    public async Task<S3ObjectContentDto?> GetS3ObjectContentAsync(string bucketName, string key, int maxBytes = 262144, string target = "localstack", CancellationToken ct = default)
    {
        return await GetAsync<S3ObjectContentDto>($"/api/v1/s3/buckets/{Uri.EscapeDataString(bucketName)}/objects/content?key={Uri.EscapeDataString(key)}&maxBytes={maxBytes}&target={target}", ct);
    }

    /// <summary>
    /// Sync S3 objects referenced in batch messages to LocalStack.
    /// </summary>
    public async Task<S3SyncResultDto?> SyncS3FromBatchAsync(S3SyncRequest request, CancellationToken ct = default)
    {
        return await PostAsync<S3SyncResultDto>("/api/v1/s3/sync-from-batch", request, ct);
    }

    #endregion

    #region Order Operations

    /// <summary>
    /// Get a single order by StoreId and OrderId.
    /// </summary>
    public async Task<object?> GetOrderByIdAsync(string storeId, string orderId, CancellationToken ct = default)
    {
        return await GetAsync<object>($"/api/v1/orders/{Uri.EscapeDataString(storeId)}/{Uri.EscapeDataString(orderId)}", ct);
    }

    /// <summary>
    /// List orders for a customer within a store.
    /// </summary>
    public async Task<object?> GetCustomerOrdersAsync(
        string storeId, string customerId, int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        return await GetAsync<object>($"/api/v1/orders/{Uri.EscapeDataString(storeId)}/customer/{Uri.EscapeDataString(customerId)}?limit={limit}&offset={offset}", ct);
    }

    /// <summary>
    /// Search orders with flexible filter criteria.
    /// </summary>
    public async Task<object?> SearchOrdersAsync(
        string storeId,
        string? customerId = null, string? channelType = null, string? fulfillmentStatus = null,
        string? orderFlow = null, string? providerName = null, string? providerId = null,
        DateTime? fromDate = null, DateTime? toDate = null,
        int limit = 50, int offset = 0,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/orders/{Uri.EscapeDataString(storeId)}/search?limit={limit}&offset={offset}";

        if (!string.IsNullOrWhiteSpace(customerId)) url += $"&customerId={Uri.EscapeDataString(customerId)}";
        if (!string.IsNullOrWhiteSpace(channelType)) url += $"&channelType={Uri.EscapeDataString(channelType)}";
        if (!string.IsNullOrWhiteSpace(fulfillmentStatus)) url += $"&fulfillmentStatus={Uri.EscapeDataString(fulfillmentStatus)}";
        if (!string.IsNullOrWhiteSpace(orderFlow)) url += $"&orderFlow={Uri.EscapeDataString(orderFlow)}";
        if (!string.IsNullOrWhiteSpace(providerName)) url += $"&providerName={Uri.EscapeDataString(providerName)}";
        if (!string.IsNullOrWhiteSpace(providerId)) url += $"&providerId={Uri.EscapeDataString(providerId)}";
        if (fromDate.HasValue) url += $"&fromDate={fromDate.Value:O}";
        if (toDate.HasValue) url += $"&toDate={toDate.Value:O}";

        return await GetAsync<object>(url, ct);
    }

    /// <summary>
    /// Get a summary of orders for a CoOrg.
    /// </summary>
    public async Task<object?> GetOrderSummaryAsync(string storeId, CancellationToken ct = default)
    {
        return await GetAsync<object>($"/api/v1/orders/{Uri.EscapeDataString(storeId)}/summary", ct);
    }

    /// <summary>
    /// Find a order by provider details.
    /// </summary>
    public async Task<object?> FindByProviderAsync(
        string storeId, string providerName, string providerOrderId, string? channelType = null, CancellationToken ct = default)
    {
        var url = $"/api/v1/orders/{Uri.EscapeDataString(storeId)}/provider/{Uri.EscapeDataString(providerName)}/{Uri.EscapeDataString(providerOrderId)}";
        if (!string.IsNullOrWhiteSpace(channelType)) url += $"?channelType={Uri.EscapeDataString(channelType)}";
        return await GetAsync<object>(url, ct);
    }

    /// <summary>
    /// List the most recent orders for a CoOrg.
    /// </summary>
    public async Task<object?> GetRecentOrdersAsync(string storeId, int limit = 20, CancellationToken ct = default)
    {
        return await GetAsync<object>($"/api/v1/orders/{Uri.EscapeDataString(storeId)}/recent?limit={limit}", ct);
    }

    #endregion

    #region HTTP Helpers

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("GET {Path}", path);
            var response = await _httpClient.GetAsync(path, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("GET {Path} failed with {StatusCode}: {Error}", path, response.StatusCode, error);
                throw new HttpRequestException($"API request failed: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GET {Path}", path);
            throw new HttpRequestException($"Failed to call API: {ex.Message}", ex);
        }
    }

    private async Task<T?> PostAsync<T>(string path, object request, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("POST {Path}", path);
            var response = await _httpClient.PostAsJsonAsync(path, request, JsonOptions, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("POST {Path} failed with {StatusCode}: {Error}", path, response.StatusCode, error);
                throw new HttpRequestException($"API request failed: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during POST {Path}", path);
            throw new HttpRequestException($"Failed to call API: {ex.Message}", ex);
        }
    }

    #endregion
}

#region DTOs

public record QueueMappingDto(
    string QueueKey,
    string DisplayName,
    string LocalStackQueueName,
    string AwsDlqName,
    string AwsSourceQueueName,
    bool Enabled
);

public record PeekedMessageDto(
    string MessageId,
    Dictionary<string, string> Attributes,
    Dictionary<string, object> MessageAttributes,
    string Body,
    int BodySize
);

public record BatchGroupDto(
    string QueueType,
    List<string> BatchIds
);

public record BatchManifestDto(
    string BatchId,
    string QueueType,
    DateTime CreatedAt,
    string SourceDlq,
    int MessageCount,
    List<string> MessageIds
);

public record SavedMessageDto(
    string MessageId,
    string Body,
    Dictionary<string, object>? MessageAttributes,
    Dictionary<string, string>? Attributes,
    string? MessageGroupId,
    DateTime DownloadedAt,
    string SourceDlq
);

public record DownloadRequest(
    string QueueKey,
    string? AwsQueueName = null,
    int MaxMessages = 100,
    string? MessageId = null
);

public record DownloadResultDto(
    int Downloaded,
    string BatchPath,
    string QueueKey,
    string AwsQueueName
);

public record ReplayFromBatchRequest(
    string QueueType,
    string BatchId,
    string? LocalStackQueueName = null
);

public record ReplayResultDto(
    int Replayed,
    int Total,
    string LocalStackQueueName
);

public record DownloadAndReplayRequest(
    string QueueKey,
    int MaxMessages = 100,
    string? MessageId = null
);

public record DownloadAndReplayResultDto(
    string QueueKey,
    int Replayed
);

public record S3BucketDto(
    string Name,
    DateTime CreationDate
);

public record S3ObjectDto(
    string Key,
    long Size,
    DateTime LastModified,
    string ETag,
    string StorageClass
);

public record S3ObjectMetadataDto(
    string Bucket,
    string Key,
    long ContentLength,
    string ContentType,
    string ETag,
    DateTime LastModified
);

public record S3ObjectContentDto(
    string Bucket,
    string Key,
    string ContentType,
    long ContentLength,
    string Content
);

public record S3SyncRequest(
    string QueueType,
    string BatchId,
    bool UseAwsFallback = true
);

public record S3SyncResultDto(
    int Synced,
    int TotalMessages,
    bool UseAwsFallback
);

#endregion
