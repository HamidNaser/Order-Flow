using System.Text;
using OrderGateway.Common.Models.Events;

namespace OrderGateway.Common.Telemetry;

public sealed class ContentSizeMetricEmitter : IContentSizeMetricEmitter
{
    private readonly Action<string> _incrementCounter;

    public ContentSizeMetricEmitter()
        : this(NewRelic.Api.Agent.NewRelic.IncrementCounter)
    {
    }

    internal ContentSizeMetricEmitter(Action<string> incrementCounter)
    {
        _incrementCounter = incrementCounter ?? throw new ArgumentNullException(nameof(incrementCounter));
    }

    public void Emit<TEvent>(string metricPrefix, string? content)
        => Emit(metricPrefix, typeof(TEvent), content);

    public void Emit(string metricPrefix, Type eventType, string? content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricPrefix);
        ArgumentNullException.ThrowIfNull(eventType);

        // Using UTF8 byte count because OrderHub uses UTF8 encoding for S3 storage.
        var byteCount = Encoding.UTF8.GetByteCount(content ?? string.Empty);
        var bucketLabel = ResolveBucket(byteCount, eventType);
        var metricName = EnsureTrailingSlash(metricPrefix) + bucketLabel;

        _incrementCounter(metricName);
    }

    private static string ResolveBucket(int byteCount, Type eventType)
    {
        return eventType switch
        {
            var type when type == typeof(OrderEvent) => ResolveContentBucket(byteCount),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unsupported content size metric type.")
        };
    }

    private static string EnsureTrailingSlash(string metricPrefix)
        => metricPrefix.EndsWith('/') ? metricPrefix : metricPrefix + "/";

    private static string ResolveContentBucket(int byteCount) => byteCount switch
    {
        0 => "0B",
        < 2_500 => "0B - 2.5KB",
        < 10_000 => "2.5KB - 10KB",
        < 25_000 => "10KB - 25KB",
        < 50_000 => "25KB - 50KB",
        < 75_000 => "50KB - 75KB",
        < 100_000 => "75KB - 100KB",
        < 150_000 => "100KB - 150KB",
        < 300_000 => "150KB - 300KB",
        < 500_000 => "300KB - 500KB",
        < 1_000_000 => "500KB - 1MB",
        < 5_000_000 => "1MB - 5MB",
        < 10_000_000 => "5MB - 10MB",
        < 25_000_000 => "10MB - 25MB",
        < 50_000_000 => "25MB - 50MB",
        _ => "50MB+"
    };

}
