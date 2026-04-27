using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;

namespace OrderGateway.Common.Processing.Pipeline;

internal sealed class ProcessingPipeline<TEvent>(IReadOnlyList<IProcessingStep<TEvent>> steps) : IProcessingPipeline<TEvent> where TEvent : IEvent
{
    public async Task<(StepResult Result, StepContext Context)> RunAsync(TEvent evt, CancellationToken ct = default)
    {
        var context = new StepContext();
        foreach (var step in steps)
        {
            var result = await step.ExecuteAsync(evt, context, ct);
            if (!result.ShouldContinue)
            {
                return (result, context);
            }
        }
        return (StepResult.Complete(), context);
    }
}
