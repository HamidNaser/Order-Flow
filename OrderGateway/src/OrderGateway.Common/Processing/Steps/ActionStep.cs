using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;

namespace OrderGateway.Common.Processing.Steps;

/// <summary>
/// A generic step that executes a provided async action.
/// By default the step will Continue, but you can supply an action that returns a StepResult
/// to control completion behavior (e.g., Complete/Retry/Poison) without creating a dedicated class.
/// </summary>
public sealed class ActionStep<TEvent> : IProcessingStep<TEvent> where TEvent : IEvent
{
    private readonly Func<TEvent, StepContext, CancellationToken, Task<StepResult>> _action;

    /// <summary>
    /// Creates an ActionStep that runs the action and then continues the pipeline.
    /// Backward-compatible with previous usage.
    /// </summary>
    public ActionStep(Func<TEvent, StepContext, CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = async (evt, ctx, ct) =>
        {
            await action(evt, ctx, ct);
            return StepResult.Continue();
        };
    }

    /// <summary>
    /// Creates an ActionStep where the action returns a StepResult to control pipeline flow.
    /// </summary>
    public ActionStep(Func<TEvent, StepContext, CancellationToken, Task<StepResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    public Task<StepResult> ExecuteAsync(TEvent evt, StepContext context, CancellationToken ct = default)
        => _action(evt, context, ct);
}
