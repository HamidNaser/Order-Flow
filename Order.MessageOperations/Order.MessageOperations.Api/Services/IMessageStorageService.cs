using Order.MessageOperations.Api.Models;

namespace Order.MessageOperations.Api.Services;

public interface IMessageStorageService
{
    string BuildBatchPath(string queueType, string batchId);

    Task<string> SaveBatchAsync(string queueType, List<SavedMessage> messages, string sourceDlq);

    Task<List<SavedMessage>> LoadBatchAsync(string batchPath);

    List<(string QueueType, List<string> Batches)> ListAvailableBatches();

    Task<MessageBatch?> LoadManifestAsync(string batchPath);
}
