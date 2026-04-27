using Amazon.SQS;
using Amazon.SQS.Model;
using Order.MessagePump.Aws.Extensions;
using Order.MessagePump.Publishers;
using Order.MessagePump.Queues;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Order.MessagePump.Aws.Queues
{
    public class SqsQueueClient : IQueueClient<Message>, IPublisherClient, IBatchPublisherClient
    {
        private readonly SqsQueueClientOptions options = null!;
        private readonly IAmazonSQS sqsClient = null!;

        // Lazy async resolution — avoids blocking the constructor thread on a network call.
        private readonly Lazy<Task<string?>> _lazyQueueUrl;
        private readonly Lazy<Task<string?>> _lazyPoisonQueueUrl;

        public SqsQueueClient(
            SqsQueueClientOptions options,
            IAmazonSQS sqsClient)
        {
            this.options = options;
            this.sqsClient = sqsClient;

            _lazyQueueUrl = new Lazy<Task<string?>>(
                () => ResolveUrlAsync(this.options.QueueUrl, this.options.QueueName));
            _lazyPoisonQueueUrl = new Lazy<Task<string?>>(
                () => ResolveUrlAsync(this.options.PoisonQueueUrl, this.options.PoisonQueueName));

            // Queue names (for ToString / logging) are derived from whatever is available at construction time.
            this.options.QueueName ??= (this.options.QueueUrl ?? string.Empty).Split('/').LastOrDefault() ?? nameof(this.options.QueueName);
            this.options.PoisonQueueName ??= (this.options.PoisonQueueUrl ?? string.Empty).Split('/').LastOrDefault() ?? nameof(this.options.PoisonQueueName);
        }

        [Obsolete("Only for mocking in unit tests")]
        public SqsQueueClient()
        {
            _lazyQueueUrl = new Lazy<Task<string?>>(() => Task.FromResult<string?>(null));
            _lazyPoisonQueueUrl = new Lazy<Task<string?>>(() => Task.FromResult<string?>(null));
        }

        /// <summary>Returns the provided URL, or resolves it from the queue name on first call.</summary>
        private async Task<string?> ResolveUrlAsync(string? explicitUrl, string? queueName)
        {
            if (!string.IsNullOrWhiteSpace(explicitUrl))
                return explicitUrl;

            return await GetQueueUrlAsync(queueName);
        }

        private Task<string?> GetQueueUrl() => _lazyQueueUrl.Value;
        private Task<string?> GetPoisonQueueUrl() => _lazyPoisonQueueUrl.Value;

        public override string ToString()
        {
            return options.QueueName ?? nameof(SqsQueueClient);
        }

        public virtual async Task<string?> GetQueueUrlAsync(string? queueName)
        {
            try
            {
                var getQueueUrlRequest = new GetQueueUrlRequest
                {
                    QueueName = queueName
                };

                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);

                getQueueUrlResponse.EnsureSuccess();

                return getQueueUrlResponse.QueueUrl;
            }
            catch (QueueDoesNotExistException)
            {
                return null;
            }
        }

        public virtual async Task CreateQueueAsync(string queueName)
        {
            var createQueueRequest = new CreateQueueRequest
            {
                QueueName = queueName
            };

            var createQueueResponse = await sqsClient.CreateQueueAsync(createQueueRequest);

            createQueueResponse.EnsureSuccess();
        }

        public virtual async Task CompleteMessageAsync(Message message)
        {
            var deleteMessageRequest = new DeleteMessageRequest
            {
                QueueUrl = await GetQueueUrl(),
                ReceiptHandle = message.ReceiptHandle
            };

            var deleteMessageResponse = await sqsClient.DeleteMessageAsync(deleteMessageRequest);

            deleteMessageResponse.EnsureSuccess();
        }

        public virtual async Task<List<Message>> GetMessagesAsync(int maxNumberOfMessages)
        {
            var receiveMessageRequest = new ReceiveMessageRequest
            {
                QueueUrl = await GetQueueUrl(),
                MaxNumberOfMessages = maxNumberOfMessages,
                WaitTimeSeconds = options.WaitTimeSeconds,
                MessageAttributeNames = new List<string> { "All" },
                MessageSystemAttributeNames = new List<string> { "All" }
            };

            var receiveMessageResponse = await sqsClient.ReceiveMessageAsync(receiveMessageRequest);

            receiveMessageResponse.EnsureSuccess();

            return receiveMessageResponse.Messages ?? new List<Message>();
        }

        public virtual async Task PoisonMessageAsync(Message message, Exception? exception = null, string? reason = null)
        {
            try
            { 
                message.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>();

                if (!string.IsNullOrWhiteSpace(exception?.Message))
                {
                    message.MessageAttributes["Exception"] = new MessageAttributeValue 
                    { 
                        DataType = "String",
                        StringValue = exception.Message
                    };
                }

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    message.MessageAttributes["Reason"] = new MessageAttributeValue 
                    { 
                        DataType = "String",
                        StringValue = reason 
                    };
                }
            }
            catch (Exception ex)
            {
                Log
                    .ForContext<SqsQueueClient>()
                    .ForContext(nameof(reason), reason)
                    .Warning(ex, "Could not append poison reason data");
            }

            await PublishMessageAsync(message.Body, message.MessageAttributes, (await GetPoisonQueueUrl())!);

            await CompleteMessageAsync(message);
        }

        public virtual async Task RetryMessageAsync(Message message, TimeSpan? backoff)
        {
            if (backoff.HasValue)
            {
                var changeMessageVisibilityRequest = new ChangeMessageVisibilityRequest
                {
                    QueueUrl = await GetQueueUrl(),
                    ReceiptHandle = message.ReceiptHandle,
                    VisibilityTimeout = (int)backoff.Value.TotalSeconds
                };

                var changeMessageVisibilityResponse = await sqsClient.ChangeMessageVisibilityAsync(changeMessageVisibilityRequest);

                changeMessageVisibilityResponse.EnsureSuccess();
            }
            else
            {
                // do nothing; use default queue VisibilityTimeout
            }
        }

        public virtual async Task<string> PublishMessageAsync(string body, Dictionary<string, string>? attributes = null)
        {
            return await PublishMessageAsync(body, attributes?.ToDictionary(a => a.Key, a => new MessageAttributeValue { DataType = "String", StringValue = a.Value }), (await GetQueueUrl())!);
        }

        private async Task<string> PublishMessageAsync(string body, Dictionary<string, MessageAttributeValue>? attributes, string queueUrl)
        {
            attributes ??= new Dictionary<string, MessageAttributeValue>();

            var delaySecondsStr = attributes.TryGetValue(nameof(SendMessageRequest.DelaySeconds), out MessageAttributeValue? a) ? a?.StringValue : null;
            var delaySeconds = int.TryParse(delaySecondsStr, out int ds) ? ds : 0;

            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = body,
                MessageAttributes = attributes,
                DelaySeconds = delaySeconds
            };

            var sendMessageResponse = await sqsClient.SendMessageAsync(sendMessageRequest);

            sendMessageResponse.EnsureSuccess();

            Log
                .ForContext<SqsQueueClient>()
                .ForContext(nameof(body), body)
                .ForContext(nameof(attributes), attributes, destructureObjects: true)
                .ForContext(nameof(queueUrl), queueUrl)
                .ForContext(nameof(sendMessageResponse.MessageId), sendMessageResponse.MessageId)
                .Verbose($"{nameof(SqsQueueClient)}.{nameof(PublishMessageAsync)}");

            return sendMessageResponse.MessageId;
        }

        public async Task<List<PublishResult>> PublishBatchMessagesAsync(List<PublishEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (entries.Count > 10)
            {
                throw new ArgumentOutOfRangeException("Entries count exceeds maximum of 10");
            }

            entries.ForEach(e => e.Id ??= Guid.NewGuid().ToString());

            var sendMessageBatchRequestEntries = entries.Select(e => new SendMessageBatchRequestEntry
            {
                Id = e.Id,
                MessageBody = e.Body,
                MessageAttributes = e.Attributes?.ToDictionary(a => a.Key, a => new MessageAttributeValue { DataType = "String", StringValue = a.Value }),

            }).ToList();

            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = sendMessageBatchRequestEntries,
                QueueUrl = await GetQueueUrl()
            };

            var sendMessageBatchResponse = await sqsClient.SendMessageBatchAsync(sendMessageBatchRequest);

            sendMessageBatchResponse.EnsureSuccess();

            Log
                .ForContext<SqsQueueClient>()
                .ForContext(nameof(sendMessageBatchResponse.Successful), sendMessageBatchResponse.Successful?.Count ?? 0)
                .ForContext(nameof(sendMessageBatchResponse.Failed), sendMessageBatchResponse.Failed?.Count ?? 0)
                .Verbose($"{nameof(SqsQueueClient)}.{nameof(PublishBatchMessagesAsync)}");

            var successful = sendMessageBatchResponse.Successful?.Select(e => new PublishResult { Id = e.Id, Success = true });
            var failed = sendMessageBatchResponse.Failed?.Select(e => new PublishResult { Id = e.Id, Success = false, Message = e.Message });
            var results = (successful ?? new List<PublishResult>()).Concat(failed ?? new List<PublishResult>()).ToList();

            return results;
        }
    }
}
