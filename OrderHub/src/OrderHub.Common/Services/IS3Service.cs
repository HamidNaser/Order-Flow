using OrderHub.Contracts.Utility;

namespace OrderHub.Common.Services;

public interface IS3Service
{
    public Task<List<string>> GetObjectKeysByPrefix(string bucketName, string prefix);
    public Task PutObjectAsync(S3PutObjectRequest request);
    public Task PutMultipartObjectAsync(S3PutMultipartObjectRequest request);
    public Task<S3GetObjectResponse> GetObjectAsync(string bucketName, string key);
    public Task<S3DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key);
    public Task PutObjectAsync<T>(S3PutObjectRequest<T> request);
    public Task<S3GetObjectResponse<T>> GetObjectAsync<T>(string bucketName, string key);
    public Task BulkDeleteObjectsAsync(string bucketName, List<string> keys);
}
