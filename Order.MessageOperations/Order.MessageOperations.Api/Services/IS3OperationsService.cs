using Order.MessageOperations.Api.Models;

namespace Order.MessageOperations.Api.Services;

public interface IS3OperationsService
{
    Task<List<S3BucketInfo>> ListBucketsAsync(bool useLocalStack, CancellationToken cancellationToken = default);

    Task<List<S3ObjectInfo>> ListObjectsAsync(
        string bucketName, string? prefix, int maxKeys, bool useLocalStack, CancellationToken cancellationToken = default);

    Task<S3ObjectMetadataInfo> GetObjectMetadataAsync(
        string bucketName, string key, bool useLocalStack, CancellationToken cancellationToken = default);

    Task<S3ObjectContentResult> GetObjectContentAsync(
        string bucketName, string key, bool useLocalStack, int maxBytes, CancellationToken cancellationToken = default);

    Task<int> SyncS3ObjectsForMessagesAsync(
        List<SavedMessage> messages, bool useAwsFallback, CancellationToken cancellationToken = default);

    Task<string> UploadObjectToLocalStackAsync(
        string bucketName, string key, string content, string contentType = "application/json", CancellationToken cancellationToken = default);
}
