namespace OrderHub.Common.Models;

/// <summary>
/// Represents the result of parsing a message payload.
/// Contains either a successfully parsed payload or error information.
/// </summary>
/// <typeparam name="TPayload">The type of payload being parsed.</typeparam>
public sealed class ParsingResult<TPayload>
{
    private ParsingResult(TPayload? payload, bool isSuccess, string? reason, Exception? exception)
    {
        Payload = payload;
        IsSuccess = isSuccess;
        Reason = reason;
        Exception = exception;
    }

    /// <summary>
    /// Gets a value indicating whether the parsing was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the successfully parsed payload. Only populated when IsSuccess is true.
    /// </summary>
    public TPayload? Payload { get; }

    /// <summary>
    /// Gets the reason for parsing failure. Only populated when IsSuccess is false.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets the exception that occurred during parsing, if any. Only populated when IsSuccess is false.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Creates a successful parsing result with the parsed payload.
    /// </summary>
    /// <param name="payload">The successfully parsed payload.</param>
    /// <returns>A successful ParsingResult containing the payload.</returns>
    public static ParsingResult<TPayload> Success(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new ParsingResult<TPayload>(payload, isSuccess: true, reason: null, exception: null);
    }

    /// <summary>
    /// Creates a failed parsing result with a reason.
    /// </summary>
    /// <param name="reason">The reason for the parsing failure.</param>
    /// <returns>A failed ParsingResult with the specified reason.</returns>
    public static ParsingResult<TPayload> Failure(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be null or whitespace.", nameof(reason));
        }

        return new ParsingResult<TPayload>(payload: default, isSuccess: false, reason: reason, exception: null);
    }

    /// <summary>
    /// Creates a failed parsing result with an exception and optional reason.
    /// </summary>
    /// <param name="exception">The exception that occurred during parsing.</param>
    /// <param name="reason">Optional reason for the parsing failure.</param>
    /// <returns>A failed ParsingResult with the specified exception and reason.</returns>
    public static ParsingResult<TPayload> Failure(Exception exception, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ParsingResult<TPayload>(payload: default, isSuccess: false, reason: reason, exception: exception);
    }

    /// <summary>
    /// Converts the parsing result to a ProcessingResult for poisoning when parsing fails.
    /// </summary>
    /// <returns>A ProcessingResult.Poison with the error details.</returns>
    public ProcessingResult ToProcessingResult()
    {
        if (IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful ParsingResult to ProcessingResult.");
        }

        return Exception != null
            ? ProcessingResult.Poison(Exception, Reason)
            : ProcessingResult.Poison(Reason);
    }
}
