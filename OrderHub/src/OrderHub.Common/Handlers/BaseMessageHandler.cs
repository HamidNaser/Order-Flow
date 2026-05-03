using Amazon.SQS.Model;
using OrderHub.Common.Configuration.Queues;
using OrderHub.Common.Models;
using OrderHub.Common.Telemetry;
using Order.MessagePump.Handlers;
using Order.MessagePump.Messages;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

namespace OrderHub.Common.Handlers;

public abstract class BaseMessageHandler<TPayload>(IOrderMetrics metrics, IOptions<MessageHandlerOptions> options) : IMessageHandler<Message>
{
    private readonly int _maxMessageRetries = options.Value.MaxMessageRetries;

    protected abstract string MessageType { get; }
    protected abstract ParsingResult<TPayload> ParsePayload(Message message);
    protected abstract Task<ProcessingResult> ProcessPayload(TPayload payload, CancellationToken cancellationToken);
    protected abstract DisposableList CreateLogContext(TPayload payload);

    public async Task<MessageResult> HandleMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        metrics.AddCustomAttribute("Custom/MessageType", MessageType);

        int receiveCount = 1;

        try
        {
            if (message.Attributes != null
                && message.Attributes.TryGetValue("ApproximateReceiveCount", out var receiveCountStr)
                && int.TryParse(receiveCountStr, out var receiveCountParsed)
               )
            {
                receiveCount = receiveCountParsed;
            }

            using var receiveCountDisposable = LogContext.PushProperty("ApproximateReceiveCount", receiveCount);

            var parsingResult = ParsePayload(message);
            if (!parsingResult.IsSuccess)
            {
                return OnPoison(parsingResult.ToProcessingResult());
            }

            var payload = parsingResult.Payload!;
            using var logContext = CreateLogContext(payload);

            var processingResult = await ProcessPayload(payload, cancellationToken);

            return processingResult.Action switch
            {
                MessageResultAction.Retry =>
                    processingResult.CompleteAfterMaxRetry
                        ? OnRetryFinal(receiveCount, processingResult)
                        : OnRetry(receiveCount, processingResult),
                MessageResultAction.Complete => OnComplete(processingResult),
                MessageResultAction.Poison => OnPoison(processingResult),
                _ => processingResult
            };
        }
        catch (Exception ex)
        {
            Log
                .ForContext<BaseMessageHandler<TPayload>>()
                .Error(ex, "Error processing {MessageType} payload", MessageType);

            metrics.IncrementCounter($"Custom/{MessageType}/Processing/Error/UnhandledException");

            var retryResult = ProcessingResult.Retry(ex, $"Unhandled exception processing {MessageType} payload.");
            return OnRetry(receiveCount, retryResult);
        }
    }

    protected virtual TimeSpan GetRetryDelay(int receiveCount)
    {
        const int maxDelaySeconds = 30;
        var attempt = Math.Max(1, receiveCount);
        var delaySeconds = Math.Min(Math.Pow(5, attempt), maxDelaySeconds);
        var jitterFactor = 0.9 + (Random.Shared.NextDouble() * 0.2); // ±10% jitter to reduce retry alignment
        var jitteredDelaySeconds = delaySeconds * jitterFactor;

        return TimeSpan.FromSeconds(jitteredDelaySeconds);
    }

    private ProcessingResult OnRetry(int approximateReceiveCount, ProcessingResult result)
    {
        if (approximateReceiveCount > _maxMessageRetries)
        {
            var reason = $"Exceeded max retries ({_maxMessageRetries}) for {MessageType} payload. Poisoning message.";
            metrics.IncrementCounter($"Custom/{MessageType}/Processing/Error/RetryLimitPoison");

            // NOTE: (explicit double logging) Logging the poison with context & currently configured to log via MessagePump (reason only).
            Log
                .ForContext<BaseMessageHandler<TPayload>>()
                .Error(
                    "MessageResultAction: {Action}, Exceeded max retries ({MaxMessageRetries}) for {MessageType} payload. Poisoning message.",
                    nameof(OnRetry),
                    _maxMessageRetries,
                    MessageType
                );

            var newResult = ProcessingResult.Poison(reason: reason);
            return OnPoison(newResult);
        }

        metrics.IncrementCounter($"Custom/{MessageType}/Processing/Result/Retry");
        result = result.WithBackoff(GetRetryDelay(approximateReceiveCount));

        return result;
    }

    private ProcessingResult OnRetryFinal(int approximateReceiveCount, ProcessingResult result)
    {
        if (approximateReceiveCount > _maxMessageRetries)
        {
            var details = $"Exceeded max retries ({_maxMessageRetries}) for {MessageType} payload. Completing message.";
            metrics.IncrementCounter($"Custom/{MessageType}/Processing/Info/RetryLimitComplete");

            Log
                .ForContext<BaseMessageHandler<TPayload>>()
                .ForContext(nameof(ProcessingResult), result, destructureObjects: true)
                .Debug(
                    "MessageResultAction: {Action}, Exceeded max retries ({MaxMessageRetries}) for {MessageType} payload. Completing message.",
                    nameof(OnRetryFinal),
                    _maxMessageRetries,
                    MessageType
                );

            var newResult = ProcessingResult.Complete(details);
            return OnComplete(newResult);
        }

        metrics.IncrementCounter($"Custom/{MessageType}/Processing/Result/Retry");
        result = result.WithBackoff(GetRetryDelay(approximateReceiveCount));

        Log
            .ForContext<BaseMessageHandler<TPayload>>()
            .ForContext(nameof(ProcessingResult), result, destructureObjects: true)
            .Debug(
                "MessageResultAction: {Action}, Retrying message for {MessageType} payload.",
                nameof(OnRetryFinal),
                MessageType
            );

        return result;
    }

    private ProcessingResult OnComplete(ProcessingResult result)
    {
        metrics.IncrementCounter(
            result.IsSuccess
                ? $"Custom/{MessageType}/Processing/Result/Processed"
                : $"Custom/{MessageType}/Processing/Result/NotProcessed"
        );

        return result;
    }

    private ProcessingResult OnPoison(ProcessingResult result)
    {
        metrics.IncrementCounter($"Custom/{MessageType}/Processing/Result/Poison");

        return result;
    }
}
