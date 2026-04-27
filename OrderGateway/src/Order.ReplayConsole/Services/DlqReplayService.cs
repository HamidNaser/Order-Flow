using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using DlqReplayTool.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DlqReplayTool.Services;

public class DlqReplayService
{
    private readonly DlqReplayConfig _config;
    private readonly ILogger<DlqReplayService> _logger;
    private readonly MessageStorageService _storageService;
    private readonly IAmazonSQS _awsSqsClient;
    private readonly IAmazonSQS _localStackSqsClient;
    private readonly IAmazonSQS? _localStackSqsFallbackClient;
    private readonly string? _localStackFallbackEndpoint;
    private readonly string _localStackSqsEndpoint;

    public DlqReplayService(
        IOptions<DlqReplayConfig> config,
        ILogger<DlqReplayService> logger,
        MessageStorageService storageService)
    {
        _config = config.Value;
        _logger = logger;
        _storageService = storageService;

        // AWS SQS client - uses default credentials from environment/profile
        // Explicitly use FallbackCredentialsFactory to support session tokens
        try
        {
            _logger.LogInformation("Attempting to load AWS credentials...");
            var awsCredentials = FallbackCredentialsFactory.GetCredentials();
            awsCredentials.GetCredentials();
            _logger.LogInformation("AWS credentials loaded successfully.");
            _logger.LogInformation("AWS credentials provider: {ProviderType}", awsCredentials.GetType().Name);
            
            _awsSqsClient = new AmazonSQSClient(
                awsCredentials,
                new AmazonSQSConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_config.AwsRegion)
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load AWS credentials. Ensure credentials are in ~/.aws/credentials or environment variables.");
            throw;
        }

        // LocalStack SQS client - always use dummy credentials and configured endpoint
        _localStackSqsEndpoint = string.IsNullOrWhiteSpace(_config.LocalStackSqsEndpoint)
            ? _config.LocalStackEndpoint
            : _config.LocalStackSqsEndpoint;

        _logger.LogInformation("LocalStack SQS config: Endpoint {Endpoint}, Region {Region}",
            _localStackSqsEndpoint,
            _config.AwsRegion);

        _localStackSqsClient = CreateLocalStackSqsClient(_localStackSqsEndpoint);
        _localStackFallbackEndpoint = GetLocalStackFallbackEndpoint(_localStackSqsEndpoint, _config.AwsRegion);
        if (!string.IsNullOrWhiteSpace(_localStackFallbackEndpoint) &&
            !string.Equals(_localStackFallbackEndpoint, _localStackSqsEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("LocalStack SQS fallback endpoint: {Endpoint}", _localStackFallbackEndpoint);
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
        _logger.LogInformation("Downloading from AWS queue: {QueueUrl}", queueUrl);

        var savedMessages = new List<SavedMessage>();
        var seenMessageIds = new HashSet<string>(); // Track messages we've already retrieved
        var messagesRetrieved = 0;
        var targetCount = maxMessages ?? int.MaxValue;
        var consecutiveEmptyBatches = 0;

        while (messagesRetrieved < targetCount && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(_config.BatchSize, targetCount - messagesRetrieved);
            var messages = await ReceiveMessagesFromDlqAsync(queueUrl, batchSize, cancellationToken);

            if (messages.Count == 0)
            {
                _logger.LogInformation("No more messages available in DLQ");
                break;
            }

            var newMessagesInBatch = 0;
            foreach (var message in messages)
            {
                // Skip if we've already seen this message
                if (seenMessageIds.Contains(message.MessageId))
                {
                    _logger.LogDebug("Skipping duplicate message: {MessageId}", message.MessageId);
                    continue;
                }

                // If looking for specific message, filter
                if (specificMessageId != null && message.MessageId != specificMessageId)
                {
                    continue;
                }

                seenMessageIds.Add(message.MessageId);
                savedMessages.Add(ConvertToSavedMessage(message, awsQueueName));
                messagesRetrieved++;
                newMessagesInBatch++;

                if (specificMessageId != null && message.MessageId == specificMessageId)
                {
                    _logger.LogInformation("Found specific message: {MessageId}", specificMessageId);
                    break;
                }

                if (messagesRetrieved >= targetCount)
                {
                    break;
                }
            }

            // If we got no new messages in this batch, all were duplicates
            if (newMessagesInBatch == 0)
            {
                consecutiveEmptyBatches++;
                _logger.LogDebug("Received {Count} duplicate messages, no new messages in batch", messages.Count);
                
                // If we get 3 consecutive batches with only duplicates, stop
                if (consecutiveEmptyBatches >= 3)
                {
                    _logger.LogInformation("No new messages after {Count} attempts, stopping download", consecutiveEmptyBatches);
                    break;
                }
            }
            else
            {
                consecutiveEmptyBatches = 0;
            }

            if (specificMessageId != null && savedMessages.Any(m => m.MessageId == specificMessageId))
            {
                break;
            }
        }

        if (savedMessages.Count == 0)
        {
            _logger.LogWarning("No messages downloaded");
            return (0, string.Empty);
        }

        // Save messages to disk
        var batchPath = await _storageService.SaveBatchAsync(queueKey, savedMessages, awsQueueName);

        _logger.LogInformation("Downloaded {Count} messages and saved to {Path}", savedMessages.Count, batchPath);
        return (savedMessages.Count, batchPath);
    }

    public async Task<int> ReplayToLocalStackAsync(
        string queueKey,
        List<SavedMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Queues.TryGetValue(queueKey, out var queueMapping))
        {
            throw new ArgumentException($"Queue '{queueKey}' not found in configuration");
        }

        string localStackQueueUrl;
        try
        {
            var getQueueUrlResponse = await _localStackSqsClient.GetQueueUrlAsync(
                queueMapping.LocalStackQueueName, cancellationToken);
            localStackQueueUrl = getQueueUrlResponse.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            _logger.LogError("Queue '{QueueName}' does not exist in LocalStack.",
                queueMapping.LocalStackQueueName);
            throw;
        }

        _logger.LogInformation("Replaying {Count} messages to LocalStack queue: {QueueUrl}",
            messages.Count, localStackQueueUrl);

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

                if (!string.IsNullOrEmpty(savedMessage.MessageGroupId))
                {
                    sendRequest.MessageGroupId = savedMessage.MessageGroupId;
                }

                var response = await _localStackSqsClient.SendMessageAsync(sendRequest, cancellationToken);
                successCount++;
                _logger.LogDebug("Replayed message {MessageId} (new ID: {NewId})", 
                    savedMessage.MessageId, response.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay message {MessageId}", savedMessage.MessageId);
            }
        }

        _logger.LogInformation("Successfully replayed {Success}/{Total} messages", successCount, messages.Count);
        return successCount;
    }

    public async Task<int> ReplayToLocalStackAsyncByName(
        string localStackQueueName,
        List<SavedMessage> messages,
        CancellationToken cancellationToken = default)
    {
        string localStackQueueUrl;
        try
        {
            var getQueueUrlResponse = await _localStackSqsClient.GetQueueUrlAsync(
                localStackQueueName, cancellationToken);
            localStackQueueUrl = getQueueUrlResponse.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            _logger.LogError("Queue '{QueueName}' does not exist in LocalStack.", localStackQueueName);
            throw;
        }

        _logger.LogInformation("Replaying {Count} messages to LocalStack queue: {QueueUrl}",
            messages.Count, localStackQueueUrl);

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

                if (!string.IsNullOrEmpty(savedMessage.MessageGroupId))
                {
                    sendRequest.MessageGroupId = savedMessage.MessageGroupId;
                }

                var response = await _localStackSqsClient.SendMessageAsync(sendRequest, cancellationToken);
                successCount++;
                _logger.LogDebug("Replayed message {MessageId} (new ID: {NewId})",
                    savedMessage.MessageId, response.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay message {MessageId}", savedMessage.MessageId);
            }
        }

        _logger.LogInformation("Successfully replayed {Success}/{Total} messages", successCount, messages.Count);
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
        return await ReplayToLocalStackAsync(queueKey, messages, cancellationToken);
    }

    public async Task<List<string>> ListLocalStackQueuesAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteLocalStackAsync(
            client => client.ListQueuesAsync(new ListQueuesRequest(), cancellationToken));
        return response.QueueUrls ?? new List<string>();
    }

    public async Task<Dictionary<string, string>> GetLocalStackQueueAttributesAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetLocalStackQueueUrlAsync(queueName, cancellationToken);
        var response = await ExecuteLocalStackAsync(
            client => client.GetQueueAttributesAsync(new GetQueueAttributesRequest
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

        var response = await ExecuteLocalStackAsync(
            client => client.ReceiveMessageAsync(request, cancellationToken));
        return response.Messages ?? new List<Message>();
    }

    private async Task<List<Message>> ReceiveMessagesFromDlqAsync(string queueUrl, int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Receiving messages from {QueueUrl} (batch size: {BatchSize})", queueUrl, batchSize);
            
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = batchSize,
                VisibilityTimeout = 300, // Hide messages for 5 minutes while we download all batches
                MessageAttributeNames = new List<string> { "All" },
                AttributeNames = new List<string> { "All" },
                WaitTimeSeconds = 0 // No long polling to avoid hanging
            };

            // Add timeout to prevent indefinite hanging
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var response = await _awsSqsClient.ReceiveMessageAsync(request, linkedCts.Token);
            var messageCount = response.Messages?.Count ?? 0;
            _logger.LogDebug("Received {Count} messages from AWS DLQ", messageCount);
            return response.Messages ?? new List<Message>();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Request timed out after 120 seconds. Check your AWS credentials and network connection.");
            return new List<Message>();
        }
        catch (Amazon.Runtime.AmazonServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError(ex, "Access denied to AWS DLQ: {QueueUrl}. Check your AWS credentials have SQS permissions.", queueUrl);
            return new List<Message>();
        }
        catch (Amazon.Runtime.AmazonClientException ex)
        {
            _logger.LogError(ex, "AWS connection error for {QueueUrl}. Check your credentials are configured.", queueUrl);
            return new List<Message>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from AWS DLQ: {QueueUrl}", queueUrl);
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
                kvp => new Models.MessageAttributeValue
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

    private string BuildAwsDlqUrl(string dlqName)
    {
        return $"https://sqs.{_config.AwsRegion}.amazonaws.com/{_config.AwsAccountId}/{dlqName}";
    }

    private string BuildAwsQueueUrl(string queueName)
    {
        return $"https://sqs.{_config.AwsRegion}.amazonaws.com/{_config.AwsAccountId}/{queueName}";
    }

    private async Task<string> GetAwsQueueUrlAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _awsSqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            return response.QueueUrl;
        }
        catch (QueueDoesNotExistException ex)
        {
            _logger.LogError(ex, "AWS queue '{QueueName}' does not exist.", queueName);
            throw;
        }
    }

    private async Task<string> GetLocalStackQueueUrlAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var response = await ExecuteLocalStackAsync(
            client => client.GetQueueUrlAsync(queueName, cancellationToken));
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
        catch (AmazonClientException ex) when (_localStackSqsFallbackClient != null)
        {
            _logger.LogWarning(ex,
                "LocalStack SQS request failed using {Endpoint}. Retrying with {FallbackEndpoint}.",
                _localStackSqsEndpoint,
                _localStackFallbackEndpoint);
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

    private string BuildLocalStackQueueUrl(string queueName)
    {
        return $"{_config.LocalStackEndpoint}/000000000000/{queueName}";
    }

    public void Dispose()
    {
        _awsSqsClient?.Dispose();
        _localStackSqsClient?.Dispose();
        _localStackSqsFallbackClient?.Dispose();
    }
}

