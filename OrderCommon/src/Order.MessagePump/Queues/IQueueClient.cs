using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.MessagePump.Queues
{
    public interface IQueueClient<T>
    {
        Task<List<T>> GetMessagesAsync(int maxNumberOfMessages);

        Task CompleteMessageAsync(T message);

        Task PoisonMessageAsync(T message, Exception? ex = null, string? reason = null);

        Task RetryMessageAsync(T message, TimeSpan? backoff = null);
    }
}
