using OrderGateway.Common.Models.Events;

namespace OrderGateway.Common.Processing.Abstractions;

public interface IProcessingPipeline<TEvent> where TEvent : IEvent
{
    Task<(StepResult Result, StepContext Context)> RunAsync(TEvent evt, CancellationToken ct = default);
}
