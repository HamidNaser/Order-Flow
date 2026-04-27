using OrderGateway.Common.FeatureToggle;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using Serilog;

namespace OrderGateway.Common.Processing.Steps;

public sealed class StoreEnabledStep<TEvent>(IFeatureToggle featureToggle) : IProcessingStep<TEvent> where TEvent : IEvent
{
    public Task<StepResult> ExecuteAsync(TEvent evt, StepContext context, CancellationToken ct = default)
    {
        var eventName = evt!.GetType().Name;
        var eventNameWithoutEvent = eventName.Replace("Event", "");

        var enabled = featureToggle.IsFeatureEnabled(
            FeatureFlags.OrderGatewayEnabledStoresV2,
            new FeatureUser { Key = nameof(StoreEnabledStep<TEvent>), StoreId = evt.StoreId }
        );

        if (enabled)
        {
            return Task.FromResult(StepResult.Continue());
        }

        Log.Debug("{PipelineStep}: Store not enabled for {Event} StoreId={StoreId}", nameof(StoreEnabledStep<TEvent>), eventName, evt.StoreId);
        NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{eventNameWithoutEvent}/Processing/Error/StoreNotEnabled");

        return Task.FromResult(StepResult.Complete("Store not enabled, skipped."));
    }
}
