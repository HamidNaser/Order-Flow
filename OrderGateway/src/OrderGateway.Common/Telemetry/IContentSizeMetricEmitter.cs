namespace OrderGateway.Common.Telemetry;

public interface IContentSizeMetricEmitter
{
    void Emit<TEvent>(string metricPrefix, string? content);

    void Emit(string metricPrefix, Type eventType, string? content);
}
