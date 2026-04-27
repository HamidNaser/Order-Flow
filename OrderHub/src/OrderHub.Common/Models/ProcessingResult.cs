using Order.MessagePump.Messages;

namespace OrderHub.Common.Models;

/// <summary>
/// Represents the result of processing a message, extending MessageResult with success indication.
/// This can be further enhanced in the future with additional processing-specific properties.
/// </summary>
public sealed class ProcessingResult : MessageResult
{
    private ProcessingResult(MessageResult result, bool completeAfterMaxRetry = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        Action = result.Action;
        Details = result.Details;
        Backoff = result.Backoff;
        Exception = result.Exception;

        IsSuccess = DetermineSuccess(result);
        CompleteAfterMaxRetry = completeAfterMaxRetry;
    }

    public bool IsSuccess { get; }

    public bool CompleteAfterMaxRetry { get; }

    private static ProcessingResult From(MessageResult result, bool completeAfterMaxRetry = false)
        => new(result, completeAfterMaxRetry);

    public static ProcessingResult Complete()
        => From(MessageResult.Complete());

    public new static ProcessingResult Complete(string details)
        => From(MessageResult.Complete(details));

    public static ProcessingResult Retry(string? details = null, bool completeAfterMaxRetry = false)
        => From(MessageResult.Retry(details: details), completeAfterMaxRetry);

    public static ProcessingResult Retry(
        Exception exception,
        string? details = null,
        TimeSpan? backoff = null,
        bool completeAfterMaxRetry = false
    )
        => From(MessageResult.Retry(exception, details, backoff), completeAfterMaxRetry);

    public static ProcessingResult Poison(string? reason = null)
        => From(MessageResult.Poison(reason: reason));

    public new static ProcessingResult Poison(Exception exception, string? reason = null)
        => From(MessageResult.Poison(exception, reason));

    /// <summary>Returns a copy with the specified backoff, preserving all other properties.</summary>
    public new ProcessingResult WithBackoff(TimeSpan backoff)
        => From(((MessageResult)this).WithBackoff(backoff), CompleteAfterMaxRetry);

    private static bool DetermineSuccess(MessageResult result)
        => result.Action == MessageResultAction.Complete && string.IsNullOrWhiteSpace(result.Details);
}
