using Amazon.SQS.Model;
using Order.MessageOperations.Api.Models;

namespace Order.MessageOperations.Api.Services;

public interface IQueueReplayService
{
    Task<(int Downloaded, string BatchPath)> DownloadFromAwsDlqAsync(
        string queueKey, int? maxMessages = null, string? specificMessageId = null, CancellationToken cancellationToken = default);

    Task<(int Downloaded, string BatchPath)> DownloadFromAwsQueueAsyncByName(
        string queueKey, string awsQueueName, int? maxMessages = null, string? specificMessageId = null, CancellationToken cancellationToken = default);

    Task<int> ReplayToLocalStackAsyncByName(
        string localStackQueueName, List<SavedMessage> messages, CancellationToken cancellationToken = default);

    Task<int> DownloadAndReplayAsync(
        string queueKey, int? maxMessages = null, string? specificMessageId = null, CancellationToken cancellationToken = default);

    Task<List<string>> ListQueuesAsync(bool useLocalStack, CancellationToken cancellationToken = default);

    Task<List<string>> ListLocalStackQueuesAsync(CancellationToken cancellationToken = default);

    Task<Dictionary<string, string>> GetQueueAttributesAsync(
        string queueName, bool useLocalStack, CancellationToken cancellationToken = default);

    Task<List<Message>> PeekMessagesAsync(
        string queueName, int maxMessages, bool useLocalStack, CancellationToken cancellationToken = default);

    Task<string> SendMessageToLocalStackAsync(
        string queueName, string messageBody, Dictionary<string, string>? messageAttributes = null,
        string? messageGroupId = null, CancellationToken cancellationToken = default);

    Task PurgeLocalStackQueueAsync(string queueName, CancellationToken cancellationToken = default);

    Task<Dictionary<string, bool>> PurgeAllConfiguredLocalStackQueuesAsync(CancellationToken cancellationToken = default);
}
