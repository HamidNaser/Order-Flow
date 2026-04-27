using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Models;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Services;

public class QueueReplayService : IDisposable
{
    private readonly MessageOperationsOptions _config;
    private readonly ILogger<QueueReplayService> _logger;
    private readonly MessageStorageService _storageService;
    private readonly IAmazonSQS _awsSqsClient;
    private readonly IAmazonSQS _localStackSqsClient;
    private readonly IAmazonSQS? _localStackSqsFallbackClient;
    private readonly string? _localStackFallbackEndpoint;
    private readonly string _localStackSqsEndpoint;

    public QueueReplayService(
        IOptions<MessageOperationsOptions> config,
        ILogger<QueueReplayService> logger,
        MessageStorageService storageService)
    {
        _config = config.Value;
        _logger = logger;
        _storageService = storageService;

        var awsCredentials = FallbackCredentialsFactory.GetCredentials();
        _awsSqsClient = new AmazonSQSClient(
            awsCredentials,
            new AmazonSQSConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_config.AwsRegion)
            });

        _localStackSqsEndpoint = string.IsNullOrWhiteSpace(_config.LocalStackSqsEndpoint)
            ? _config.LocalStackEndpoint
            : _config.LocalStackSqsEndpoint;

        _localStackSqsClient = CreateLocalStackSqsClient(_localStackSqsEndpoint);
        _localStackFallbackEndpoint = GetLocalStackFallbackEndpoint(_localStackSqsEndpoint, _config.AwsRegion);
        if (!string.IsNullOrWhiteSpace(_localStackFallbackEndpoint)
            && !string.Equals(_localStackFallbackEndpoint, _localStackSqsEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            _localStackSqsFallbackClient = CreateLocalStackSqsClient(_localStackFallbackEndpoint);
        }
    }

    public async Task<(int Downloaded, string BatchPath)> DownloadFromAwsDlqAsync(
        string queueKey,
        int? maxMessages = null,
        string? specificMessageId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Queues.TryGetValue(queueKey, out var queueMapping))
        {
            throw new ArgumentException($"Queue '{queueKey}' not found in configuration");
        }

        return await DownloadFromAwsQueueAsyncByName(
            queueKey,
            queueMapping.AwsDlqName,
            maxMessages,
            specificMessageId,
            cancellationToken);
    }

    public async Task<(int Downloaded, string BatchPath)> DownloadFromAwsQueueAsyncByName(
        string queueKey,
        string awsQueueName,
        int? maxMessages = null,
        string? specificMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetAwsQueueUrlAsync(awsQueueName, cancellationToken);

        var savedMessages = new List<SavedMessage>();
        var seenMessageIds = new HashSet<string>();
        var messagesRetrieved = 0;
        var targetCount = maxMessages ?? int.MaxValue;
        var consecutiveDuplicateBatches = 0;

        while (messagesRetrieved < targetCount && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(_config.BatchSize, targetCount - messagesRetrieved);
            var messages = await ReceiveMessagesFromDlqAsync(queueUrl, batchSize, cancellationToken);

            if (messages.Count == 0)
            {
                break;
            }

            var newMessagesInBatch = 0;
            foreach (var message in messages)
            {
                if (seenMessageIds.Contains(message.MessageId))
                {
                    continue;
                }

                if (specificMessageId is not null && message.MessageId != specificMessageId)
                {
                    continue;
                }

                seenMessageIds.Add(message.MessageId);
                savedMessages.Add(ConvertToSavedMessage(message, awsQueueName));
                messagesRetrieved++;
                newMessagesInBatch++;

                if (specificMessageId is not null && message.MessageId == specificMessageId)
                {
                    break;
                }

                if (messagesRetrieved >= targetCount)
                {
                    break;
                }
            }

            if (newMessagesInBatch == 0)
            {
                consecutiveDuplicateBatches++;
                if (consecutiveDuplicateBatches >= 3)
                {
                    break;
                }
            }
            else
            {
                consecutiveDuplicateBatches = 0;
            }

            if (specificMessageId is not null && savedMessages.Any(m => m.MessageId == specificMessageId))
            {
                break;
            }
        }

        if (!savedMessages.Any())
        {
            return (0, string.Empty);
        }

        var batchPath = await _storageService.SaveBatchAsync(queueKey, savedMessages, awsQueueName);
        return (savedMessages.Count, batchPath);
    }

    public async Task<int> ReplayToLocalStackAsyncByName(
        string localStackQueueName,
        List<SavedMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var getQueueUrlResponse = await _localStackSqsClient.GetQueueUrlAsync(localStackQueueName, cancellationToken);
        var localStackQueueUrl = getQueueUrlResponse.QueueUrl;

        var successCount = 0;
        foreach (var savedMessage in messages)
        {
            try
            {
                var sendRequest = new SendMessageRequest
                {
                    QueueUrl = localStackQueueUrl,
                    MessageBody = savedMessage.Body,
                    MessageAttributes = savedMessage.MessageAttributes.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new Amazon.SQS.Model.MessageAttributeValue
                        {
                            StringValue = kvp.Value.StringValue,
                            DataType = kvp.Value.DataType ?? "String"
                        })
                };

                if (!string.IsNullOrWhiteSpace(savedMessage.MessageGroupId))
                {
                    sendRequest.MessageGroupId = savedMessage.MessageGroupId;
                }

                await _localStackSqsClient.SendMessageAsync(sendRequest, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay message {MessageId}", savedMessage.MessageId);
            }
        }

        return successCount;
    }

    public async Task<int> DownloadAndReplayAsync(
        string queueKey,
        int? maxMessages = null,
        string? specificMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var (downloaded, batchPath) = await DownloadFromAwsDlqAsync(queueKey, maxMessages, specificMessageId, cancellationToken);
        if (downloaded == 0)
        {
            return 0;
        }

        var messages = await _storageService.LoadBatchAsync(batchPath);
        if (!_config.Queues.TryGetValue(queueKey, out var queueMapping))
        {
            throw new ArgumentException($"Queue '{queueKey}' not found in configuration");
        }

        return await ReplayToLocalStackAsyncByName(queueMapping.LocalStackQueueName, messages, cancellationToken);
    }

    public async Task<List<string>> ListLocalStackQueuesAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteLocalStackAsync(client => client.ListQueuesAsync(new ListQueuesRequest(), cancellationToken));
        return response.QueueUrls ?? new List<string>();
    }

    public async Task<Dictionary<string, string>> GetLocalStackQueueAttributesAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetLocalStackQueueUrlAsync(queueName, cancellationToken);
        var response = await ExecuteLocalStackAsync(client => client.GetQueueAttributesAsync(
            new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = new List<string> { "All" }
            }, cancellationToken));

        return response.Attributes ?? new Dictionary<string, string>();
    }

    public async Task<List<Message>> PeekLocalStackMessagesAsync(
        string queueName,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetLocalStackQueueUrlAsync(queueName, cancellationToken);
        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = Math.Clamp(maxMessages, 1, 10),
            WaitTimeSeconds = 0,
            VisibilityTimeout = 0,
            MessageAttributeNames = new List<string> { "All" },
            AttributeNames = new List<string> { "All" }
        };

        var response = await ExecuteLocalStackAsync(client => client.ReceiveMessageAsync(request, cancellationToken));
        return response.Messages ?? new List<Message>();
    }

    #region Target-aware methods (LocalStack or AWS)

    /// <summary>
    /// List queues from either LocalStack or AWS based on the useLocalStack flag.
    /// </summary>
    public async Task<List<string>> ListQueuesAsync(bool useLocalStack, CancellationToken cancellationToken = default)
    {
        if (useLocalStack)
        {
            return await ListLocalStackQueuesAsync(cancellationToken);
        }

        var response = await _awsSqsClient.ListQueuesAsync(new ListQueuesRequest(), cancellationToken);
        return response.QueueUrls ?? new List<string>();
    }

    /// <summary>
    /// Get queue attributes from either LocalStack or AWS based on the useLocalStack flag.
    /// </summary>
    public async Task<Dictionary<string, string>> GetQueueAttributesAsync(
        string queueName, bool useLocalStack, CancellationToken cancellationToken = default)
    {
        if (useLocalStack)
        {
            return await GetLocalStackQueueAttributesAsync(queueName, cancellationToken);
        }

        var queueUrl = await GetAwsQueueUrlAsync(queueName, cancellationToken);
        var response = await _awsSqsClient.GetQueueAttributesAsync(
            new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = new List<string> { "All" }
            }, cancellationToken);

        return response.Attributes ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Peek at messages from either LocalStack or AWS based on the useLocalStack flag.
    /// Uses VisibilityTimeout=0 so messages remain on the queue.
    /// </summary>
    public async Task<List<Message>> PeekMessagesAsync(
        string queueName, int maxMessages, bool useLocalStack, CancellationToken cancellationToken = default)
    {
        if (useLocalStack)
        {
            return await PeekLocalStackMessagesAsync(queueName, maxMessages, cancellationToken);
        }

        var queueUrl = await GetAwsQueueUrlAsync(queueName, cancellationToken);
        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = Math.Clamp(maxMessages, 1, 10),
            WaitTimeSeconds = 0,
            VisibilityTimeout = 0,
            MessageAttributeNames = new List<string> { "All" },
            AttributeNames = new List<string> { "All" }
        };

        var response = await _awsSqsClient.ReceiveMessageAsync(request, cancellationToken);
        return response.Messages ?? new List<Message>();
    }

    #endregion

    private async Task<List<Message>> ReceiveMessagesFromDlqAsync(
        string queueUrl,
        int batchSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = batchSize,
                VisibilityTimeout = 300,
                MessageAttributeNames = new List<string> { "All" },
                AttributeNames = new List<string> { "All" },
                WaitTimeSeconds = 0
            };

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var response = await _awsSqsClient.ReceiveMessageAsync(request, linkedCts.Token);
            return response.Messages ?? new List<Message>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from AWS queue {QueueUrl}", queueUrl);
            return new List<Message>();
        }
    }

    private SavedMessage ConvertToSavedMessage(Message message, string sourceDlq)
    {
        return new SavedMessage
        {
            MessageId = message.MessageId,
            Body = message.Body,
            MessageAttributes = message.MessageAttributes.ToDictionary(
                kvp => kvp.Key,
                kvp => new MessageAttributeValueModel
                {
                    StringValue = kvp.Value.StringValue,
                    DataType = kvp.Value.DataType
                }),
            Attributes = message.Attributes,
            MessageGroupId = message.Attributes.TryGetValue("MessageGroupId", out var groupId) ? groupId : null,
            ReceiptHandle = message.ReceiptHandle,
            DownloadedAt = DateTime.UtcNow,
            SourceDlq = sourceDlq
        };
    }

    private async Task<string> GetAwsQueueUrlAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var response = await _awsSqsClient.GetQueueUrlAsync(queueName, cancellationToken);
        return response.QueueUrl;
    }

    private async Task<string> GetLocalStackQueueUrlAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var response = await ExecuteLocalStackAsync(client => client.GetQueueUrlAsync(queueName, cancellationToken));
        return response.QueueUrl;
    }

    private IAmazonSQS CreateLocalStackSqsClient(string serviceUrl)
    {
        var credentials = new BasicAWSCredentials("test-access-key-123", "test-secret-access-key-456");
        return new AmazonSQSClient(
            credentials,
            new AmazonSQSConfig
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = _config.AwsRegion,
                UseHttp = true
            });
    }

    private async Task<T> ExecuteLocalStackAsync<T>(Func<IAmazonSQS, Task<T>> action)
    {
        try
        {
            return await action(_localStackSqsClient);
        }
        catch (AmazonClientException) when (_localStackSqsFallbackClient is not null)
        {
            return await action(_localStackSqsFallbackClient);
        }
    }

    private static string? GetLocalStackFallbackEndpoint(string endpoint, string region)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var hostPrefix = $"sqs.{region}.";
        if (!uri.Host.StartsWith(hostPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var newHost = uri.Host.Substring(hostPrefix.Length);
        var builder = new UriBuilder(uri)
        {
            Host = newHost
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    public void Dispose()
    {
        _awsSqsClient.Dispose();
        _localStackSqsClient.Dispose();
        _localStackSqsFallbackClient?.Dispose();
    }
}
