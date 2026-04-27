using Amazon.SQS.Model;
using Order.MessagePump.Publishers;
using Order.MessagePump.Queues;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Order.MessagePump.Aws.Queues
{
    public class LocalQueueClient : 
        IQueueClient<Message>, 
        IPublisherClient, 
        IBatchPublisherClient,
        ITestSubscriberClient
    {
        public ConcurrentQueue<Message> Queue { get; set; }

        public ConcurrentQueue<Message> PoisonQueue { get; set; }

        public LocalQueueClient()
        {
            Queue = new ConcurrentQueue<Message>();
            PoisonQueue = new ConcurrentQueue<Message>();
        }

        public async Task CompleteMessageAsync(Message message)
        {
            await Task.CompletedTask;

            // do nothing; message is already dequeued
        }

        public async Task<List<Message>> GetMessagesAsync(int maxNumberOfMessages)
        {
            await Task.CompletedTask;

            var messages = new List<Message>();

            for (int i = 0; i < maxNumberOfMessages; i++)
            {
                if (Queue.TryDequeue(out Message? m))
                {
                    messages.Add(m);
                }
            }

            return messages;
        }

        public async Task PoisonMessageAsync(Message message, Exception? ex = null, string? reason = null)
        {
            await Task.CompletedTask;

            PoisonQueue.Enqueue(message);
        }

        public async Task RetryMessageAsync(Message message, TimeSpan? backoff = null)
        {
            await Task.CompletedTask;

            Queue.Enqueue(message);
        }

        public async Task<string> PublishMessageAsync(string body, Dictionary<string, string>? attributes = null)
        {
            await Task.CompletedTask;

            var message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = body,
                MessageAttributes = attributes?.ToDictionary(
                    a => a.Key,
                    a => new MessageAttributeValue { StringValue = a.Value, DataType = "String" })
            };

            Queue.Enqueue(message);

            return message.MessageId;
        }

        public async Task<List<PublishResult>> PublishBatchMessagesAsync(List<PublishEntry> entries)
        {
            foreach (var entry in entries)
            {
                await PublishMessageAsync(entry.Body, entry.Attributes);
            }

            return entries.Select(e => new PublishResult { }).ToList();
        }

        public Task<List<string>> FindMessagesAsync(string messageContains)
        {
            throw new NotImplementedException();
        }
    }
}
