using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Order.MessagePump.Queues
{
    public interface IQueueClient<T>
    {
        Task<List<T>> GetMessagesAsync(int maxNumberOfMessages, CancellationToken cancellationToken = default);

        Task CompleteMessageAsync(T message, CancellationToken cancellationToken = default);

        Task PoisonMessageAsync(T message, Exception? ex = null, string? reason = null, CancellationToken cancellationToken = default);

        Task RetryMessageAsync(T message, TimeSpan? backoff = null, CancellationToken cancellationToken = default);
    }
}
