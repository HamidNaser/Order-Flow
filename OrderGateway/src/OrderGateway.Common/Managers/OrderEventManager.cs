using Order.MessagePump.Publishers;
using OrderGateway.Common.Configuration.Queues;
using OrderGateway.Common.FeatureToggle;
using OrderGateway.Common.Models;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Processing.Pipeline;
using OrderGateway.Common.Processing.Steps;
using OrderGateway.Common.Services;
using OrderGateway.Common.Telemetry;
using Serilog;

namespace OrderGateway.Common.Managers;

public class OrderEventManager(
    IFeatureToggle featureToggle,
    ICloudContentService cloudContentService,
    IOrderService orderService,
    IContentSizeMetricEmitter contentSizeMetricEmitter
) : IOrderEventManager
{
    public async Task<ProcessingResult> ProcessEvent(OrderEvent orderEvent)
    {
        Log
            .ForContext<OrderEventManager>()
            .Debug(nameof(ProcessEvent));

        LogNewRelicMetrics(orderEvent);

        var steps = new List<IProcessingStep<OrderEvent>>
        {
            new ValidateStep<OrderEvent>(),
            new ActionStep<OrderEvent>(async (evt, _, _) =>
            {
                NewRelic.Api.Agent.NewRelic.IncrementCounter(
                    evt.IsStandardPriority
                        ? "Custom/Order/Priority/Standard"
                        : "Custom/Order/Priority/Express"
                );

                await Task.CompletedTask;
            }),
            new StoreEnabledStep<OrderEvent>(featureToggle),
            new RetrieveOrderContentStep(cloudContentService, contentSizeMetricEmitter),
            new ActionStep<OrderEvent>((evt, ctx, _) =>
            {
                var title = evt.GetMetadataValue("OrderTitle");
                var hasTitle = !string.IsNullOrWhiteSpace(title);
                if (string.IsNullOrWhiteSpace(ctx.MessageContent))
                {
                    NewRelic.Api.Agent.NewRelic.IncrementCounter(
                        hasTitle
                            ? "Custom/Order/Processing/Info/NoBody"
                            : "Custom/Order/Processing/Info/NoTitleAndNoBody");
                }

                return Task.FromResult(StepResult.Continue());
            }),
            new SendOrderStep<OrderEvent>(orderService)
        };

        var pipeline = new ProcessingPipeline<OrderEvent>(steps);
        (StepResult stepResult, StepContext context) = await pipeline.RunAsync(orderEvent);

        return ProcessingResult.From(stepResult, context);
    }

    private void LogNewRelicMetrics(OrderEvent orderEvent)
    {
        // --- Pattern A: numeric metadata fields (Set/NotSet based on valid parse) ---
        EmitNumericPresence<int>(orderEvent, "StoreId", int.TryParse, v => v > 0);
        EmitNumericPresence<int>(orderEvent, "UserId", int.TryParse, v => v > 0);
        EmitNumericPresence<long>(orderEvent, "SourceTrackingId", long.TryParse, v => v > 0);
        EmitNumericPresence<int>(orderEvent, "TrackingRef", int.TryParse, v => v > 0);
        EmitNumericPresence<int>(orderEvent, "CustomerId", int.TryParse, v => v > 0);

        // --- Pattern B: string metadata fields (Set/NotSet based on non-blank) ---
        string[] stringFields = ["MessageId", "OrderReferenceId", "OrderTitle", "RecipientAddress", "SenderAddress", "MessageCloudContentKey", "VideoMedia"];
        foreach (var field in stringFields)
        {
            EmitSetOrNotSet(field, !string.IsNullOrWhiteSpace(orderEvent.GetMetadataValue(field)));
        }

        // --- Special cases ---

        // Description lives on the object, not in metadata
        EmitSetOrNotSet("Description", !string.IsNullOrWhiteSpace(orderEvent.Description));

        // Classification: Set/NotSet + per-value counter
        var classification = orderEvent.GetMetadataValue("Classification");
        EmitSetOrNotSet("Classification", !string.IsNullOrWhiteSpace(classification));
        if (!string.IsNullOrWhiteSpace(classification))
        {
            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/Order/Classification/{classification}");
        }

        // OriginalMessage: Set/NotSet + content-size bucketing
        var originalMessage = orderEvent.GetMetadataValue("OriginalMessage");
        EmitSetOrNotSet("OriginalMessage", !string.IsNullOrWhiteSpace(originalMessage));
        if (!string.IsNullOrWhiteSpace(originalMessage))
        {
            contentSizeMetricEmitter.Emit<OrderEvent>("Custom/Order/ContentSize/Original/", originalMessage);
        }

        // HasAttachments: Set/NotSet + attachment-specific counters
        var hasAttachmentsValue = orderEvent.GetMetadataValue("HasAttachments");
        EmitSetOrNotSet("HasAttachments", !string.IsNullOrWhiteSpace(hasAttachmentsValue));
        if (!string.IsNullOrWhiteSpace(hasAttachmentsValue) && hasAttachmentsValue.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            NewRelic.Api.Agent.NewRelic.IncrementCounter("Custom/Order/HasAttachments");
            if (orderEvent.IsStandardPriority)
            {
                NewRelic.Api.Agent.NewRelic.IncrementCounter("Custom/Order/HasAttachmentsAndIsAutomated");
            }
        }

        // OrderFlowType: per-value direction counter only (no Set/NotSet)
        var orderFlowType = orderEvent.GetMetadataValue("OrderFlowType");
        if (!string.IsNullOrWhiteSpace(orderFlowType))
        {
            NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/Order/Direction/{orderFlowType}");
        }
    }

    private static void EmitSetOrNotSet(string fieldName, bool isSet)
    {
        NewRelic.Api.Agent.NewRelic.IncrementCounter(
            isSet ? $"Custom/Order/Set/{fieldName}" : $"Custom/Order/NotSet/{fieldName}"
        );
    }

    private delegate bool TryParseDelegate<T>(string? s, out T result);

    private static void EmitNumericPresence<T>(OrderEvent orderEvent, string fieldName, TryParseDelegate<T> tryParse, Func<T, bool> isValid)
    {
        var raw = orderEvent.GetMetadataValue(fieldName);
        var present = !string.IsNullOrWhiteSpace(raw) && tryParse(raw, out var parsed) && isValid(parsed);
        EmitSetOrNotSet(fieldName, present);
    }
}
