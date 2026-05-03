using Order.MessageOperations.Api.Models;
using Order.MessageOperations.Api.Models.Responses;

namespace Order.MessageOperations.Api.Services;

public interface ITraceService
{
    /// <summary>
    /// Poll LocalStack S3 until an object matching the key prefix appears, or timeout.
    /// </summary>
    Task<TraceS3Result> WaitForS3ObjectAsync(
        string bucketName, string keyPrefix, int timeoutSeconds = 30, int pollIntervalMs = 500, CancellationToken ct = default);

    /// <summary>
    /// Poll a LocalStack SQS queue until a message (optionally containing bodyContains) appears, or timeout.
    /// </summary>
    Task<TraceQueueResult> WaitForQueueMessageAsync(
        string queueName, string? bodyContains = null, int timeoutSeconds = 30, int pollIntervalMs = 500, CancellationToken ct = default);

    /// <summary>
    /// Poll MongoDB until a document matching the filter appears for the given store, or timeout.
    /// </summary>
    Task<TraceMongoResult> WaitForMongoDocumentAsync(
        string storeId, string? providerOrderId = null, string? customerId = null,
        int timeoutSeconds = 30, int pollIntervalMs = 500, CancellationToken ct = default);

    /// <summary>
    /// Get the approximate message count for all configured LocalStack queues.
    /// </summary>
    Task<AllQueueDepthsResult> GetAllQueueDepthsAsync(CancellationToken ct = default);
}
