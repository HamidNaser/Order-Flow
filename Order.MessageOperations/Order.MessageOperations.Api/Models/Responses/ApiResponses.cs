using Amazon.SQS.Model;

namespace Order.MessageOperations.Api.Models.Responses;

// ── Shared ────────────────────────────────────────────────────────
/// <summary>Standard error envelope returned by all endpoints.</summary>
public record ErrorResponse(string Message);

// ── Batches ───────────────────────────────────────────────────────
public record BatchGroupDto(string QueueType, List<string> BatchIds);

// ── Queues ────────────────────────────────────────────────────────
public record QueueConfigDto(
    string QueueKey,
    string DisplayName,
    string? LocalStackQueueName,
    string? AwsDlqName,
    string? AwsSourceQueueName,
    bool Enabled);

public record PeekedMessageDto(
    string MessageId,
    Dictionary<string, string> Attributes,
    Dictionary<string, MessageAttributeValue> MessageAttributes,
    string? Body,
    int BodySize);

// ── Orders ────────────────────────────────────────────────────────
public record CustomerOrdersResponse(
    string StoreId,
    string CustomerId,
    long TotalCount,
    int Returned,
    int Limit,
    int Offset,
    List<OrderRecord> Orders);

public record CustomerOrderCountResponse(
    string StoreId,
    string CustomerId,
    long Count);

public record OrderSearchResponse(
    string StoreId,
    OrderSearchParams Filters,
    int Returned,
    List<OrderRecord> Orders);

public record RecentOrdersResponse(
    string StoreId,
    int Returned,
    List<OrderRecord> Orders);

// ── Replay ────────────────────────────────────────────────────────
public record DownloadMessagesResponse(
    int Downloaded,
    string BatchPath,
    string QueueKey,
    string AwsQueueName);

public record ReplayFromBatchResponse(
    int Replayed,
    int Total,
    string? LocalStackQueueName = null);

public record DownloadAndReplayResponse(
    string QueueKey,
    int Replayed);

// ── S3 ────────────────────────────────────────────────────────────
public record SyncFromBatchResponse(
    int Synced,
    int TotalMessages,
    bool UseAwsFallback);

// ── Queue Write ───────────────────────────────────────────────────
public record SendMessageResponse(
    string QueueName,
    string MessageId);

public record PurgeQueueResponse(
    string QueueName,
    bool Success);

public record PurgeAllQueuesResponse(
    int Purged,
    int Failed,
    Dictionary<string, bool> Results);

// ── S3 Write ──────────────────────────────────────────────────────
public record UploadS3ObjectResponse(
    string BucketName,
    string Key,
    string ETag);

// ── Health ────────────────────────────────────────────────────────
public record LocalStackHealthResponse(
    bool Healthy,
    LocalStackServiceStatus Sqs,
    LocalStackServiceStatus S3,
    string LocalStackEndpoint);

public record LocalStackServiceStatus(
    bool Healthy,
    string? Detail);

// ── Trace / Polling ───────────────────────────────────────────────
public record TraceS3Result(
    bool Found,
    string BucketName,
    string KeyPrefix,
    int ElapsedMs,
    int TimeoutMs,
    string? MatchedKey,
    long? Size,
    string? Detail);

public record TraceQueueResult(
    bool Found,
    string QueueName,
    string? BodyContains,
    int ElapsedMs,
    int TimeoutMs,
    string? MessageId,
    string? BodyPreview,
    string? Detail);

public record TraceMongoResult(
    bool Found,
    string StoreId,
    string? ProviderOrderId,
    string? CustomerId,
    int ElapsedMs,
    int TimeoutMs,
    string? MatchedOrderId,
    string? Detail);

public record QueueDepthEntry(
    string QueueKey,
    string QueueName,
    int ApproximateMessageCount,
    int ApproximateNotVisible);

public record AllQueueDepthsResult(
    List<QueueDepthEntry> Queues,
    int TotalMessages);

// ── Test Data Generation ──────────────────────────────────────────
public record GeneratedOrder(
    int Index,
    string OrderReferenceId,
    string StoreId,
    string Priority,
    string ChannelType,
    string Format,
    string TargetQueue,
    string Body,
    string Description);

public record GenerateOrdersResponse(
    int Count,
    string Priority,
    string ChannelType,
    string Format,
    string TargetQueue,
    List<GeneratedOrder> Orders);
