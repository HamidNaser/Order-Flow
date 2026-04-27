using OrderGateway.Common.Models.Events;

namespace OrderGateway.Common.Processing.Abstractions;

public interface IProcessingStep<TEvent> where TEvent : IEvent
{
    Task<StepResult> ExecuteAsync(TEvent evt, StepContext context, CancellationToken ct = default);
}

public sealed class StepContext
{
    public string? MessageContent { get; set; }
    public string? OrderId { get; set; }
}
