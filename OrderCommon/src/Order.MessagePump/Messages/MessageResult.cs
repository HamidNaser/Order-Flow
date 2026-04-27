using System;

namespace Order.MessagePump.Messages
{
    public class MessageResult
    {
        public MessageResultAction Action { get; init; }

        public string? Details { get; init; }

        public TimeSpan? Backoff { get; init; }

        public Exception? Exception { get; init; }

        // handy -- but not required -- result initializers

        public static MessageResult Complete(string? details = null) => new MessageResult
        {
            Action = MessageResultAction.Complete,
            Details = details
        };

        public static MessageResult Poison(Exception? ex = null, string? reason = null) => new MessageResult
        {
            Action = MessageResultAction.Poison,
            Exception = ex,
            Details = reason
        };

        public static MessageResult Retry(Exception? ex = null, string? details = null, TimeSpan? backoff = null) => new MessageResult
        {
            Action = MessageResultAction.Retry,
            Exception = ex,
            Details = details,
            Backoff = backoff
        };

        /// <summary>Returns a copy with the specified backoff, preserving all other properties.</summary>
        public MessageResult WithBackoff(TimeSpan backoff) => new MessageResult
        {
            Action = Action,
            Details = Details,
            Exception = Exception,
            Backoff = backoff
        };
    }
}
