using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using Serilog;

namespace OrderGateway.Common.Processing.Steps;

public sealed class ValidateStep<TEvent> : IProcessingStep<TEvent> where TEvent : IEvent
{
    public Task<StepResult> ExecuteAsync(TEvent evt, StepContext context, CancellationToken ct = default)
    {
        var eventName = evt!.GetType().Name;
        var eventNameWithoutEvent = eventName.Replace("Event", "");

        if (evt.IsValid())
        {
            return Task.FromResult(StepResult.Continue());
        }

        var validationErrors = evt.GetValidationErrors();
        var errorSummary = string.Join("; ", validationErrors);
        
        Log.Debug("{PipelineStep}: Validation failed for {Event} StoreId={StoreId}. Errors: {ValidationErrors}", 
            nameof(ValidateStep<TEvent>), eventName, evt.StoreId, errorSummary);

        // Emit detailed per-field counters (separated from validation logic)
        evt.EmitValidationCounters();
        NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{eventNameWithoutEvent}/Processing/Error/InvalidOrder");

        return Task.FromResult(StepResult.Complete("Order event failed validation"));
    }
}
