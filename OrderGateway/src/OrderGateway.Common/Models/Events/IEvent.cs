using OrderGateway.Common.Telemetry;

namespace OrderGateway.Common.Models.Events;

public interface IEvent
{
    int StoreId { get; }
    int ApproximateReceiveCount { get; set; }
    bool IsValid();
    IReadOnlyList<string> GetValidationErrors();

    /// <summary>
    /// Emits telemetry counters for validation findings.
    /// Separated from <see cref="IsValid"/>/<see cref="GetValidationErrors"/>
    /// so that validation itself remains a pure computation.
    /// </summary>
    void EmitValidationCounters(IOrderMetrics metrics);
}
