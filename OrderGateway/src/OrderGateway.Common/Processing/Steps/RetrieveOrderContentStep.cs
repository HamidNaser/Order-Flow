using Order.MessagePump.Messages;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Services;
using OrderGateway.Common.Telemetry;
using Serilog;

namespace OrderGateway.Common.Processing.Steps;

public sealed class RetrieveOrderContentStep(
    ICloudContentService cloudContentService,
    IContentSizeMetricEmitter contentSizeMetricEmitter,
    IOrderMetrics metrics
) : IProcessingStep<OrderEvent>
{
    public async Task<StepResult> ExecuteAsync(OrderEvent evt, StepContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt.Metadata);

        // OriginalMessage already present; Cloud Content lookup is not needed.
        var original = evt.GetMetadataValue("OriginalMessage");
        if (!string.IsNullOrWhiteSpace(original))
        {
            context.MessageContent = original;

            return StepResult.Continue();
        }

        var key = evt.GetMetadataValue("MessageCloudContentKey");
        if (string.IsNullOrWhiteSpace(key))
        {
            return StepResult.Continue();
        }

        try
        {
            var content = await cloudContentService.ReadContentAsync(key, ct);
            if (content == null)
            {
                metrics.IncrementCounter("Custom/Order/Processing/Info/CloudContentNotFound");
                Log.Warning("{PipelineStep}: Cloud content not found for key {Key}", nameof(RetrieveOrderContentStep), key);
                return StepResult.Continue();
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                metrics.IncrementCounter("Custom/Order/Processing/Info/CloudContentEmpty");
                return StepResult.Continue();
            }

            context.MessageContent = content;
            contentSizeMetricEmitter.Emit<OrderEvent>("Custom/Order/ContentSize/Cloud/", content);
            return StepResult.Continue();
        }
        catch (Exception ex)
        {
            metrics.IncrementCounter("Custom/Order/Processing/Error/CloudContentFailure");
            Log.Debug(ex, "{PipelineStep}: Cloud content retrieval failure for key {Key}", nameof(RetrieveOrderContentStep), key);

            return StepResult.Complete(MessageResult.Poison(reason: $"Cloud content retrieval failure for key {key}."));
        }
    }
}
