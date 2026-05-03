namespace OrderHub.Common.Telemetry;

/// <summary>
/// Abstraction over application-performance monitoring counters and transaction attributes.
/// Inject this instead of calling the static NewRelic API directly so that handlers and
/// services remain unit-testable without a live APM agent.
/// </summary>
public interface IOrderMetrics
{
    void IncrementCounter(string counterName);
    void AddCustomAttribute(string name, object value);
}
