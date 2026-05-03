using System.Diagnostics;
using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Models.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Services;

/// <summary>
/// Polling-based trace service for observing data flow through LocalStack and MongoDB.
/// Used by the AI agent to verify that messages sent to queues arrive in downstream systems.
/// </summary>
public class TraceService : ITraceService
{
    private readonly IQueueReplayService _queueReplayService;
    private readonly IS3OperationsService _s3OperationsService;
    private readonly IOrderQueryService? _orderQueryService;
    private readonly MessageOperationsOptions _options;
    private readonly ILogger<TraceService> _logger;

    public TraceService(
        IQueueReplayService queueReplayService,
        IS3OperationsService s3OperationsService,
        IOptions<MessageOperationsOptions> options,
        ILogger<TraceService> logger,
        IOrderQueryService? orderQueryService = null)
    {
        _queueReplayService = queueReplayService;
        _s3OperationsService = s3OperationsService;
        _options = options.Value;
        _logger = logger;
        _orderQueryService = orderQueryService;
    }

    public async Task<TraceS3Result> WaitForS3ObjectAsync(
        string bucketName, string keyPrefix, int timeoutSeconds = 30, int pollIntervalMs = 500, CancellationToken ct = default)
    {
        var timeoutMs = timeoutSeconds * 1000;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("Trace: Waiting for S3 object in {Bucket} with prefix '{Prefix}' (timeout={Timeout}s)",
            bucketName, keyPrefix, timeoutSeconds);

        while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
        {
            try
            {
                var objects = await _s3OperationsService.ListObjectsAsync(
                    bucketName, keyPrefix, maxKeys: 1, useLocalStack: true, ct);

                if (objects.Count > 0)
                {
                    sw.Stop();
                    _logger.LogInformation("Trace: Found S3 object '{Key}' after {Elapsed}ms", objects[0].Key, sw.ElapsedMilliseconds);
                    return new TraceS3Result(
                        Found: true,
                        BucketName: bucketName,
                        KeyPrefix: keyPrefix,
                        ElapsedMs: (int)sw.ElapsedMilliseconds,
                        TimeoutMs: timeoutMs,
                        MatchedKey: objects[0].Key,
                        Size: objects[0].Size,
                        Detail: $"Found after {sw.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Trace: S3 poll error (will retry)");
            }

            await Task.Delay(pollIntervalMs, ct);
        }

        sw.Stop();
        return new TraceS3Result(
            Found: false,
            BucketName: bucketName,
            KeyPrefix: keyPrefix,
            ElapsedMs: (int)sw.ElapsedMilliseconds,
            TimeoutMs: timeoutMs,
            MatchedKey: null,
            Size: null,
            Detail: $"Timed out after {sw.ElapsedMilliseconds}ms");
    }

    public async Task<TraceQueueResult> WaitForQueueMessageAsync(
        string queueName, string? bodyContains = null, int timeoutSeconds = 30, int pollIntervalMs = 500, CancellationToken ct = default)
    {
        var timeoutMs = timeoutSeconds * 1000;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("Trace: Waiting for message in queue '{Queue}' (bodyContains='{Body}', timeout={Timeout}s)",
            queueName, bodyContains, timeoutSeconds);

        while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
        {
            try
            {
                var messages = await _queueReplayService.PeekMessagesAsync(
                    queueName, maxMessages: 10, useLocalStack: true, ct);

                foreach (var msg in messages)
                {
                    if (string.IsNullOrEmpty(bodyContains) ||
                        (msg.Body?.Contains(bodyContains, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        sw.Stop();
                        var preview = msg.Body?.Length > 200 ? msg.Body[..200] + "..." : msg.Body;
                        _logger.LogInformation("Trace: Found matching message '{MsgId}' after {Elapsed}ms", msg.MessageId, sw.ElapsedMilliseconds);
                        return new TraceQueueResult(
                            Found: true,
                            QueueName: queueName,
                            BodyContains: bodyContains,
                            ElapsedMs: (int)sw.ElapsedMilliseconds,
                            TimeoutMs: timeoutMs,
                            MessageId: msg.MessageId,
                            BodyPreview: preview,
                            Detail: $"Found after {sw.ElapsedMilliseconds}ms");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Trace: Queue poll error (will retry)");
            }

            await Task.Delay(pollIntervalMs, ct);
        }

        sw.Stop();
        return new TraceQueueResult(
            Found: false,
            QueueName: queueName,
            BodyContains: bodyContains,
            ElapsedMs: (int)sw.ElapsedMilliseconds,
            TimeoutMs: timeoutMs,
            MessageId: null,
            BodyPreview: null,
            Detail: $"Timed out after {sw.ElapsedMilliseconds}ms");
    }

    public async Task<TraceMongoResult> WaitForMongoDocumentAsync(
        string storeId, string? providerOrderId = null, string? customerId = null,
        int timeoutSeconds = 30, int pollIntervalMs = 500, CancellationToken ct = default)
    {
        if (_orderQueryService == null)
        {
            return new TraceMongoResult(
                Found: false, StoreId: storeId, ProviderOrderId: providerOrderId, CustomerId: customerId,
                ElapsedMs: 0, TimeoutMs: timeoutSeconds * 1000, MatchedOrderId: null,
                Detail: "MongoDB is not configured. Set the MongoDB connection string to enable document tracing.");
        }

        var timeoutMs = timeoutSeconds * 1000;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("Trace: Waiting for MongoDB document (storeId={StoreId}, provider={Provider}, customer={Customer}, timeout={Timeout}s)",
            storeId, providerOrderId, customerId, timeoutSeconds);

        while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
        {
            try
            {
                // Strategy: if providerOrderId is given, search by provider; else search recent by customer
                if (!string.IsNullOrWhiteSpace(providerOrderId))
                {
                    var order = await _orderQueryService.FindByProviderAsync(
                        storeId, providerOrderId, providerName: "any", ct: ct);

                    if (order != null)
                    {
                        sw.Stop();
                        _logger.LogInformation("Trace: Found order '{OrderId}' by provider after {Elapsed}ms", order.OrderId, sw.ElapsedMilliseconds);
                        return new TraceMongoResult(
                            Found: true, StoreId: storeId, ProviderOrderId: providerOrderId, CustomerId: customerId,
                            ElapsedMs: (int)sw.ElapsedMilliseconds, TimeoutMs: timeoutMs,
                            MatchedOrderId: order.OrderId,
                            Detail: $"Found by providerOrderId after {sw.ElapsedMilliseconds}ms");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(customerId))
                {
                    var orders = await _orderQueryService.GetRecentAsync(storeId, limit: 1, ct: ct);

                    if (orders.Count > 0)
                    {
                        sw.Stop();
                        _logger.LogInformation("Trace: Found recent order '{OrderId}' after {Elapsed}ms", orders[0].OrderId, sw.ElapsedMilliseconds);
                        return new TraceMongoResult(
                            Found: true, StoreId: storeId, ProviderOrderId: providerOrderId, CustomerId: customerId,
                            ElapsedMs: (int)sw.ElapsedMilliseconds, TimeoutMs: timeoutMs,
                            MatchedOrderId: orders[0].OrderId,
                            Detail: $"Found recent order after {sw.ElapsedMilliseconds}ms");
                    }
                }
                else
                {
                    // No filter - just check for any recent order
                    var orders = await _orderQueryService.GetRecentAsync(storeId, limit: 1, ct: ct);
                    if (orders.Count > 0)
                    {
                        sw.Stop();
                        return new TraceMongoResult(
                            Found: true, StoreId: storeId, ProviderOrderId: null, CustomerId: null,
                            ElapsedMs: (int)sw.ElapsedMilliseconds, TimeoutMs: timeoutMs,
                            MatchedOrderId: orders[0].OrderId,
                            Detail: $"Found recent order after {sw.ElapsedMilliseconds}ms");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Trace: MongoDB poll error (will retry)");
            }

            await Task.Delay(pollIntervalMs, ct);
        }

        sw.Stop();
        return new TraceMongoResult(
            Found: false, StoreId: storeId, ProviderOrderId: providerOrderId, CustomerId: customerId,
            ElapsedMs: (int)sw.ElapsedMilliseconds, TimeoutMs: timeoutMs,
            MatchedOrderId: null,
            Detail: $"Timed out after {sw.ElapsedMilliseconds}ms");
    }

    public async Task<AllQueueDepthsResult> GetAllQueueDepthsAsync(CancellationToken ct = default)
    {
        var entries = new List<QueueDepthEntry>();
        var totalMessages = 0;

        foreach (var (key, queueConfig) in _options.Queues)
        {
            if (!queueConfig.Enabled || string.IsNullOrWhiteSpace(queueConfig.LocalStackQueueName))
                continue;

            try
            {
                var attrs = await _queueReplayService.GetQueueAttributesAsync(
                    queueConfig.LocalStackQueueName, useLocalStack: true, ct);

                var msgCount = int.TryParse(
                    attrs.GetValueOrDefault("ApproximateNumberOfMessages", "0"), out var mc) ? mc : 0;
                var notVisible = int.TryParse(
                    attrs.GetValueOrDefault("ApproximateNumberOfMessagesNotVisible", "0"), out var nv) ? nv : 0;

                entries.Add(new QueueDepthEntry(key, queueConfig.LocalStackQueueName, msgCount, notVisible));
                totalMessages += msgCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Trace: Failed to get depth for queue '{Queue}'", queueConfig.LocalStackQueueName);
                entries.Add(new QueueDepthEntry(key, queueConfig.LocalStackQueueName, -1, -1));
            }
        }

        return new AllQueueDepthsResult(entries, totalMessages);
    }
}
