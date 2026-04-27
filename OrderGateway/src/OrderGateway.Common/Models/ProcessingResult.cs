using System.Diagnostics.CodeAnalysis;
using Order.MessagePump.Messages;
using OrderGateway.Common.Processing;
using OrderGateway.Common.Processing.Abstractions;

namespace OrderGateway.Common.Models;

public sealed class ProcessingResult : MessageResult
{
    [SetsRequiredMembers]
    private ProcessingResult(MessageResult result, StepContext context)
    {
        ArgumentNullException.ThrowIfNull(result);

        Action = result.Action;
        Details = result.Details;
        Backoff = result.Backoff;
        Exception = result.Exception;

        StepContext = context;
        IsSuccess = DetermineSuccess(result);
    }

    public required StepContext StepContext { get; init; }

    public bool IsSuccess { get; }

    public static ProcessingResult From(MessageResult result, StepContext? context = null)
        => new(result, context ?? new StepContext());

    public static ProcessingResult From(StepResult stepResult, StepContext? context = null)
        => From(stepResult.Result ?? MessageResult.Complete(), context ?? new StepContext());

    public static ProcessingResult Complete(StepContext? context = null)
        => From(MessageResult.Complete(), context);

    public static ProcessingResult Complete(string details, StepContext? context = null)
        => From(MessageResult.Complete(details), context);

    public static ProcessingResult Retry(string? details = null, StepContext? context = null)
        => From(MessageResult.Retry(details: details), context);

    public static ProcessingResult Retry(Exception exception, string? details = null, TimeSpan? backoff = null, StepContext? context = null)
        => From(MessageResult.Retry(exception, details, backoff), context);

    public static ProcessingResult Poison(string? reason = null, StepContext? context = null)
        => From(MessageResult.Poison(reason: reason), context);

    public static ProcessingResult Poison(Exception exception, string? reason = null, StepContext? context = null)
        => From(MessageResult.Poison(exception, reason), context);

    /// <summary>Returns a copy with the specified backoff, preserving all other properties.</summary>
    public new ProcessingResult WithBackoff(TimeSpan backoff)
        => From(((MessageResult)this).WithBackoff(backoff), StepContext);

    private static bool DetermineSuccess(MessageResult result)
        => result.Action == MessageResultAction.Complete && string.IsNullOrWhiteSpace(result.Details);
}
