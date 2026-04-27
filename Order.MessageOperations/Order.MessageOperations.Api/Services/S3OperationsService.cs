using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Models;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Services;

public class S3OperationsService : IDisposable
{
    private readonly IAmazonS3 _awsS3Client;
    private readonly IAmazonS3 _localStackS3Client;
    private readonly ILogger<S3OperationsService> _logger;
    private readonly MessageOperationsOptions _config;
    private readonly string _cacheRoot;

    public S3OperationsService(
        ILogger<S3OperationsService> logger,
        IOptions<MessageOperationsOptions> config)
    {
        _logger = logger;
        _config = config.Value;
        _cacheRoot = ResolveCacheRoot(_config);

        var awsCredentials = FallbackCredentialsFactory.GetCredentials();
        _awsS3Client = new AmazonS3Client(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(_config.AwsRegion));

        var localStackS3Endpoint = string.IsNullOrWhiteSpace(_config.LocalStackS3Endpoint)
            ? _config.LocalStackEndpoint
            : _config.LocalStackS3Endpoint;

        _localStackS3Client = new AmazonS3Client(
            new BasicAWSCredentials("test-access-key-123", "test-secret-access-key-456"),
            new AmazonS3Config
            {
                ServiceURL = localStackS3Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = _config.AwsRegion
            });

        EnsureCacheDirectoryExists();
    }

    public async Task<List<S3BucketInfo>> ListBucketsAsync(bool useLocalStack, CancellationToken cancellationToken = default)
    {
        var client = useLocalStack ? _localStackS3Client : _awsS3Client;
        var response = await client.ListBucketsAsync(cancellationToken);

        return response.Buckets
            .Select(bucket => new S3BucketInfo
            {
                Name = bucket.BucketName,
                CreationDate = bucket.CreationDate ?? DateTime.MinValue
            })
            .OrderBy(bucket => bucket.Name)
            .ToList();
    }

    public async Task<List<S3ObjectInfo>> ListObjectsAsync(
        string bucketName,
        string? prefix,
        int maxKeys,
        bool useLocalStack,
        CancellationToken cancellationToken = default)
    {
        var client = useLocalStack ? _localStackS3Client : _awsS3Client;
        var response = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
            MaxKeys = Math.Clamp(maxKeys, 1, 1000)
        }, cancellationToken);

        return response.S3Objects.Select(item => new S3ObjectInfo
        {
            Key = item.Key,
            Size = item.Size ?? 0,
            LastModified = item.LastModified ?? DateTime.MinValue,
            ETag = item.ETag,
            StorageClass = item.StorageClass?.Value
        }).ToList();
    }

    public async Task<S3ObjectMetadataInfo> GetObjectMetadataAsync(
        string bucketName,
        string key,
        bool useLocalStack,
        CancellationToken cancellationToken = default)
    {
        var client = useLocalStack ? _localStackS3Client : _awsS3Client;
        var response = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = bucketName,
            Key = key
        }, cancellationToken);

        return new S3ObjectMetadataInfo
        {
            Bucket = bucketName,
            Key = key,
            ContentLength = response.ContentLength,
            ContentType = response.Headers.ContentType,
            ETag = response.ETag,
            LastModified = response.LastModified ?? DateTime.MinValue
        };
    }

    public async Task<S3ObjectContentResult> GetObjectContentAsync(
        string bucketName,
        string key,
        bool useLocalStack,
        int maxBytes,
        CancellationToken cancellationToken = default)
    {
        var client = useLocalStack ? _localStackS3Client : _awsS3Client;
        using var response = await client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucketName,
            Key = key
        }, cancellationToken);

        var cappedBytes = Math.Clamp(maxBytes, 1024, 1_048_576);
        using var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);

        var bytes = memoryStream.ToArray();
        var outputBytes = bytes.Length > cappedBytes ? bytes[..cappedBytes] : bytes;
        var content = Encoding.UTF8.GetString(outputBytes);

        return new S3ObjectContentResult
        {
            Bucket = bucketName,
            Key = key,
            ContentType = response.Headers.ContentType,
            ContentLength = response.Headers.ContentLength,
            Content = content
        };
    }

    public async Task<int> SyncS3ObjectsForMessagesAsync(
        List<SavedMessage> messages,
        bool useAwsFallback,
        CancellationToken cancellationToken = default)
    {
        var s3References = ExtractS3References(messages);

        if (s3References.Count == 0)
        {
            return 0;
        }

        var syncedCount = 0;
        foreach (var s3Reference in s3References)
        {
            try
            {
                var cacheFilePath = GetCacheFilePath(s3Reference);

                if (File.Exists(cacheFilePath))
                {
                    await UploadCachedObjectAsync(s3Reference, cacheFilePath, cancellationToken);
                    syncedCount++;
                    continue;
                }

                if (!useAwsFallback)
                {
                    continue;
                }

                var getRequest = new GetObjectRequest
                {
                    BucketName = s3Reference.Bucket,
                    Key = s3Reference.Key
                };

                using var response = await _awsS3Client.GetObjectAsync(getRequest, cancellationToken);
                var cacheDirectory = Path.GetDirectoryName(cacheFilePath);
                if (!string.IsNullOrWhiteSpace(cacheDirectory))
                {
                    Directory.CreateDirectory(cacheDirectory);
                }

                await using (var cacheStream = File.Create(cacheFilePath))
                {
                    await response.ResponseStream.CopyToAsync(cacheStream, cancellationToken);
                }

                await UploadCachedObjectAsync(s3Reference, cacheFilePath, cancellationToken, response.Headers.ContentType);
                syncedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync S3 object s3://{Bucket}/{Key}", s3Reference.Bucket, s3Reference.Key);
            }
        }

        return syncedCount;
    }

    private async Task UploadCachedObjectAsync(
        S3Reference s3Reference,
        string cacheFilePath,
        CancellationToken cancellationToken,
        string? contentType = null)
    {
        await using var fileStream = File.OpenRead(cacheFilePath);
        var putRequest = new PutObjectRequest
        {
            BucketName = s3Reference.Bucket,
            Key = s3Reference.Key,
            InputStream = fileStream,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
        };

        await _localStackS3Client.PutObjectAsync(putRequest, cancellationToken);
    }

    private List<S3Reference> ExtractS3References(List<SavedMessage> messages)
    {
        var references = new List<S3Reference>();

        foreach (var message in messages)
        {
            try
            {
                var bodyDoc = JsonDocument.Parse(message.Body);
                if (!bodyDoc.RootElement.TryGetProperty("Records", out var records))
                {
                    continue;
                }

                foreach (var record in records.EnumerateArray())
                {
                    if (!record.TryGetProperty("s3", out var s3Element))
                    {
                        continue;
                    }

                    var bucket = s3Element.GetProperty("bucket").GetProperty("name").GetString();
                    var key = s3Element.GetProperty("object").GetProperty("key").GetString();

                    if (!string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(key))
                    {
                        references.Add(new S3Reference
                        {
                            Bucket = bucket,
                            Key = key
                        });
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        return references
            .GroupBy(reference => $"{reference.Bucket}/{reference.Key}")
            .Select(group => group.First())
            .ToList();
    }

    private string ResolveCacheRoot(MessageOperationsOptions config)
    {
        if (!string.IsNullOrWhiteSpace(config.S3CachePath))
        {
            return config.S3CachePath;
        }

        return Path.Combine(config.MessageStoragePath, "s3-cache");
    }

    private string GetCacheFilePath(S3Reference s3Reference)
    {
        var normalizedKey = Uri.UnescapeDataString(s3Reference.Key)
            .Replace('\\', '/')
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.Combine(_cacheRoot, s3Reference.Bucket, normalizedKey);
    }

    private void EnsureCacheDirectoryExists()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            Directory.CreateDirectory(_cacheRoot);
        }
    }

    public void Dispose()
    {
        _awsS3Client.Dispose();
        _localStackS3Client.Dispose();
    }

    private class S3Reference
    {
        public required string Bucket { get; set; }
        public required string Key { get; set; }
    }
}
