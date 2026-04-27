using Amazon.S3.Util;
using Amazon.SQS.Model;
using OrderHub.Common.Configuration.Queues;
using OrderHub.Common.Models;
using OrderHub.Common.Models.OrderMappers;
using OrderHub.Common.Repositories;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;
using OrderHub.Contracts.Utility;
using Microsoft.Extensions.Options;
using Serilog;
using FulfillmentStatus = OrderHub.Common.Models.Components.FulfillmentStatus;
using Serilog.Context;
using System.Diagnostics;

namespace OrderHub.Common.Handlers;

public class OrderHandler(
    IS3Service s3Service,
    IContentProcessingService contentProcessingService,
    IOrderMapper orderMapper,
    IOrderRepository repository,
    ICustomerLockService customerLockService,
    IOptions<MessageHandlerOptions> options
) : BaseMessageHandler<OrderHandler.OrderPayload>(options)
{
    protected override string MessageType => "Order";

    public record OrderPayload(
        string BucketName,
        string Key,
        S3OrderKey ParsedKey
    );

    protected override ParsingResult<OrderPayload> ParsePayload(Message message)
    {
        // Check for S3 test event (operational no-op)
        // S3 bucket notifications emit a test event when notifications are configured.
        // These are expected infrastructure events and should not be processed as business messages.
        if (IsS3TestEvent(message.Body))
        {
            Log.ForContext<OrderHandler>()
                .Information("Received S3 test event notification; acknowledging");
            
            // Return a marker payload that ProcessPayload will recognize and complete immediately.
            // The null fields serve as a sentinel to indicate this is a test event.
            return ParsingResult<OrderPayload>.Success(
                new OrderPayload(BucketName: null!, Key: null!, ParsedKey: null!)
            );
        }

        S3EventNotification? s3EventNotification;
        try
        {
            s3EventNotification = S3EventNotification.ParseJson(message.Body);
        }
        catch (Exception ex)
        {
            return ParsingResult<OrderPayload>.Failure(ex, "Failed to parse S3 event notification.");
        }

        if (s3EventNotification?.Records?.Count != 1)
        {
            return ParsingResult<OrderPayload>.Failure("Unexpected number of records");
        }

        var record = s3EventNotification.Records.Single();
        var bucketName = record.S3.Bucket.Name;
        var key = record.S3.Object.Key;

        if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
        {
            return ParsingResult<OrderPayload>.Failure($"Invalid BucketName: {bucketName} or Key: {key}");
        }

        var validKey = S3OrderKey.TryParse(key, out var parsedKey);
        if (!validKey)
        {
            return ParsingResult<OrderPayload>.Failure($"Invalid Key Format: {key}");
        }

        var payload = new OrderPayload(
            bucketName,
            key,
            parsedKey!
        );

        return ParsingResult<OrderPayload>.Success(payload);
    }

    protected override async Task<ProcessingResult> ProcessPayload(OrderPayload payload)
    {
        // Handle S3 test event marker (no processing needed)
        // Test events are emitted when S3 bucket notifications are configured and are not business events.
        if (payload.BucketName == null || payload.ParsedKey == null)
        {
            return ProcessingResult.Complete();
        }

        var getObjectResponse = await s3Service.GetObjectAsync<OrderRequest>(payload.BucketName, payload.Key);

        if (getObjectResponse.ErrorType != S3ErrorType.NONE)
        {
            return ProcessingResult.Poison($"S3 retrieval failed: {getObjectResponse.ErrorMessage}");
        }

        if (getObjectResponse.Content == null)
        {
            return ProcessingResult.Poison("S3 object content is null");
        }

        var contentProcessingResult = contentProcessingService.ProcessContent(
            payload.ParsedKey.ChannelType,
            getObjectResponse.Content.Content ?? string.Empty
        );

        var channelOrder = orderMapper.ToInternalModel(
            getObjectResponse.Content,
            payload.ParsedKey.OrderId,
            contentProcessingResult,
            payload.ParsedKey.Priority
        );

        var log = Log
            .ForContext<OrderHandler>()
            .ForContext(nameof(ChannelOrder), channelOrder, destructureObjects: true);

        log.Debug("Processing order payload");

        var customerId = getObjectResponse.Content.CustomerId;

        var lease = await TryAcquireCustomerLockAsync(log, customerId);
        if (!lease.IsAcquired)
        {
            log.Warning("Customer lock not acquired; retrying ingestion insert");

            return ProcessingResult.Retry("Lock acquisition failed");
        }

        try
        {
            await repository.InsertAsync(channelOrder);
        }
        finally
        {
            var releaseStopwatch = Stopwatch.StartNew();

            await customerLockService.ReleaseLocksAsync(lease);

            releaseStopwatch.Stop();

            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{MessageType}/Lock/Released");

            using (LogContext.PushProperty("lockReleasedMs", releaseStopwatch.ElapsedMilliseconds))
            {
                log.Debug("Customer lock released after ingestion insert");
            }
        }

        return ProcessingResult.Complete();
    }

    private async Task<ICustomerLockLease> TryAcquireCustomerLockAsync(
        ILogger log,
        string customerId)
    {
        var acquireStopwatch = Stopwatch.StartNew();
        var lease = await customerLockService.AcquireLocksAsync([customerId]);
        acquireStopwatch.Stop();

        using (LogContext.PushProperty("customerId", customerId))
        using (LogContext.PushProperty("lockAcquiredMs", acquireStopwatch.ElapsedMilliseconds))
        {
            if (!lease.IsAcquired)
            {
                NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{MessageType}/Lock/Failed");
                return lease;
            }

            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{MessageType}/Lock/Acquired");

            log.Debug("Customer lock acquired for ingestion");

            var agent = NewRelic.Api.Agent.NewRelic.GetAgent();
            agent.CurrentTransaction.AddCustomAttribute("Custom/LockAcquisitionTimeMs", acquireStopwatch.ElapsedMilliseconds);

            return lease;
        }
    }

    protected override DisposableList CreateLogContext(OrderPayload payload)
    {
        return
        [
            LogContext.PushProperty("Payload", payload, destructureObjects: true)
        ];
    }

    /// <summary>
    /// Detects whether the message body represents an S3 test event.
    /// S3 emits test events when bucket notifications are configured or updated.
    /// These are operational events and should not trigger business logic processing.
    /// </summary>
    private static bool IsS3TestEvent(string messageBody)
    {
        try
        {
            // S3 test events contain an "Event" field with value "s3:TestEvent"
            // Regular S3 object events have a "Records" array with event details
            return messageBody.Contains("\"Event\"") && messageBody.Contains("\"s3:TestEvent\"");
        }
        catch
        {
            return false;
        }
    }
}
