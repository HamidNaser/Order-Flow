namespace OrderGateway.Common.Telemetry;

/// <summary>
/// Production implementation that delegates to the NewRelic .NET agent.
/// </summary>
public sealed class NewRelicOrderMetrics : IOrderMetrics
{
    public void IncrementCounter(string counterName)
        => NewRelic.Api.Agent.NewRelic.IncrementCounter(counterName);

    public void AddCustomAttribute(string name, object value)
        => NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute(name, value);
}
