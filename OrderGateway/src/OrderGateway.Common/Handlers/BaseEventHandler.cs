using Amazon.SQS.Model;
using Order.MessagePump.Handlers;
using Order.MessagePump.Messages;
using OrderGateway.Common.Configuration.Handlers;
using OrderGateway.Common.Models.Events;
using Microsoft.Extensions.Options;
using NewRelic.Api.Agent;
using Serilog;
using OrderGateway.Common.Models;

namespace OrderGateway.Common.Handlers;

public abstract class BaseEventHandler<TEvent>(IOptions<MessageHandlerOptions> options) : IMessageHandler<Message> where TEvent : IEvent
{
    private readonly IAgent _agent = NewRelic.Api.Agent.NewRelic.GetAgent();
    private readonly int _maxMessageRetries = options.Value.MaxMessageRetries;

    protected abstract string EventType { get; }
    protected internal abstract TEvent ParseEvent(Message message);
    protected abstract Task<ProcessingResult> ProcessEvent(TEvent evt);
    protected abstract DisposableList CreateLogContext(TEvent evt);

    public async Task<MessageResult> HandleMessageAsync(Message message)
    {
        _agent.CurrentTransaction.AddCustomAttribute("Custom/EventType", EventType);

        TEvent evt;
        try
        {
            evt = ParseEvent(message);
            EnrichReceiveCount(message, evt);
        }
        catch (Exception ex)
        {
            var result = ProcessingResult.Poison(ex, $"Failed to Parse{typeof(TEvent).Name}");
            return OnPoison(result);
        }

        using var logContext = CreateLogContext(evt);

        try
        {
            var result = await ProcessEvent(evt);

            return result.Action switch
            {
                MessageResultAction.Retry => OnRetry(evt, result),
                MessageResultAction.Complete => OnComplete(result),
                MessageResultAction.Poison => OnPoison(result),
                _ => result
            };
        }
        catch (Exception ex)
        {
            Log
                .ForContext<BaseEventHandler<TEvent>>()
                .Error(ex, "Error processing {EventType} event", EventType);

            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{EventType}/Processing/Error/UnhandledException");

            var result = ProcessingResult.Retry(ex, $"Unhandled exception processing {EventType} event.");
            return OnRetry(evt, result);
        }
    }

    protected virtual void EnrichReceiveCount(Message message, TEvent evt)
    {
        if (message.Attributes != null &&
            message.Attributes.TryGetValue("ApproximateReceiveCount", out var receiveCountStr) &&
            int.TryParse(receiveCountStr, out var receiveCount))
        {
            evt.ApproximateReceiveCount = receiveCount;
        }
    }

    protected virtual TimeSpan GetRetryDelay(TEvent evt)
    {
        const int maxDelaySeconds = 30;
        var attempt = Math.Max(1, evt.ApproximateReceiveCount);
        var delaySeconds = Math.Min(Math.Pow(5, attempt), maxDelaySeconds);
        var jitterFactor = 0.9 + (Random.Shared.NextDouble() * 0.2); // ±10% jitter to reduce retry alignment
        return TimeSpan.FromSeconds(delaySeconds * jitterFactor);
    }

    private ProcessingResult OnRetry(TEvent evt, ProcessingResult result)
    {
        if (evt.ApproximateReceiveCount > _maxMessageRetries)
        {
            var reason = $"Exceeded max retries ({_maxMessageRetries}) for {EventType} event. Poisoning message.";
            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{EventType}/Processing/Error/RetryLimitPoison");

            // NOTE: (explicit double logging) Logging the poison with context & currently configured to log via MessagePump (reason only).
            Log
                .ForContext<BaseEventHandler<TEvent>>()
                .Error(
                    "MessageResultAction: {Action}, Exceeded max retries ({MaxMessageRetries}) for {EventType} event. Poisoning message.",
                    nameof(OnRetry),
                    _maxMessageRetries,
                    EventType
                );

            var newResult = ProcessingResult.Poison(reason: reason, context: result.StepContext);
            return OnPoison(newResult);
        }

        NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{EventType}/Processing/Result/Retry");

        return result.WithBackoff(GetRetryDelay(evt));
    }

    private ProcessingResult OnComplete(ProcessingResult result)
    {
        if (result.IsSuccess)
        {
            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{EventType}/Processing/Result/Ingested");
        }
        else
        {
            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{EventType}/Processing/Result/NotIngested");
        }

        return result;
    }

    private ProcessingResult OnPoison(ProcessingResult result)
    {
        NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{EventType}/Processing/Result/Poison");

        return result;
    }
}
