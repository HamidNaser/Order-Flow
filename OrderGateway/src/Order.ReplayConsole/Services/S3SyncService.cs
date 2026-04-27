using Amazon.S3;
using Amazon.S3.Model;
using DlqReplayTool.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DlqReplayTool.Services;

public class S3SyncService
{
    private readonly IAmazonS3 _awsS3Client;
    private readonly IAmazonS3 _localStackS3Client;
    private readonly ILogger<S3SyncService> _logger;
    private readonly DlqReplayConfig _config;
    private readonly string _cacheRoot;
    private readonly string _localStackS3Endpoint;

    public S3SyncService(
        ILogger<S3SyncService> logger,
        IOptions<DlqReplayConfig> config)
    {
        _logger = logger;
        _config = config.Value;
        _cacheRoot = ResolveCacheRoot(_config);
        _localStackS3Endpoint = string.IsNullOrWhiteSpace(_config.LocalStackS3Endpoint)
            ? _config.LocalStackEndpoint
            : _config.LocalStackS3Endpoint;

        // AWS S3 client - uses default credentials
        _awsS3Client = new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(_config.AwsRegion));

        // LocalStack S3 client
        _logger.LogInformation("LocalStack S3 config: Endpoint {Endpoint}, Region {Region}",
            _localStackS3Endpoint,
            _config.AwsRegion);
        _localStackS3Client = new AmazonS3Client(
            new Amazon.Runtime.BasicAWSCredentials("test-access-key-123", "test-secret-access-key-456"),
            new AmazonS3Config
            {
                ServiceURL = _localStackS3Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = _config.AwsRegion
            });

        EnsureCacheDirectoryExists();
    }

    public async Task<int> SyncS3ObjectsForMessagesAsync(
        List<SavedMessage> messages,
        bool useAwsFallback,
        CancellationToken cancellationToken = default)
    {
        var s3References = ExtractS3References(messages);
        
        if (s3References.Count == 0)
        {
            _logger.LogInformation("No S3 references found in messages");
            return 0;
        }

        _logger.LogInformation("Found {Count} S3 object(s) to sync to LocalStack (AWS fallback: {Fallback})",
            s3References.Count,
            useAwsFallback);

        var syncedCount = 0;
        foreach (var s3Ref in s3References)
        {
            try
            {
                var cacheFilePath = GetCacheFilePath(s3Ref);

                if (File.Exists(cacheFilePath))
                {
                    await UploadCachedObjectAsync(s3Ref, cacheFilePath, cancellationToken);
                    syncedCount++;
                    continue;
                }

                if (!useAwsFallback)
                {
                    _logger.LogWarning("Cached S3 object not found: s3://{Bucket}/{Key}", s3Ref.Bucket, s3Ref.Key);
                    continue;
                }

                // Download from AWS S3 and cache locally
                _logger.LogDebug("Downloading s3://{Bucket}/{Key} from AWS", s3Ref.Bucket, s3Ref.Key);
                var getRequest = new GetObjectRequest
                {
                    BucketName = s3Ref.Bucket,
                    Key = s3Ref.Key
                };

                using var response = await _awsS3Client.GetObjectAsync(getRequest, cancellationToken);
                var cacheDirectory = Path.GetDirectoryName(cacheFilePath);
                if (!string.IsNullOrEmpty(cacheDirectory))
                {
                    Directory.CreateDirectory(cacheDirectory);
                }

                await using (var cacheStream = File.Create(cacheFilePath))
                {
                    await response.ResponseStream.CopyToAsync(cacheStream, cancellationToken);
                }

                await UploadCachedObjectAsync(s3Ref, cacheFilePath, cancellationToken, response.Headers.ContentType);
                syncedCount++;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("S3 object not found in AWS: s3://{Bucket}/{Key}", s3Ref.Bucket, s3Ref.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync S3 object: s3://{Bucket}/{Key}", s3Ref.Bucket, s3Ref.Key);
            }
        }

        _logger.LogInformation("Synced {Synced}/{Total} S3 objects to LocalStack", syncedCount, s3References.Count);
        return syncedCount;
    }

    private void EnsureCacheDirectoryExists()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            Directory.CreateDirectory(_cacheRoot);
            _logger.LogInformation("Created S3 cache directory: {Path}", _cacheRoot);
        }
    }

    private async Task UploadCachedObjectAsync(
        S3Reference s3Ref,
        string cacheFilePath,
        CancellationToken cancellationToken,
        string? contentType = null)
    {
        _logger.LogDebug("Uploading cached object to LocalStack s3://{Bucket}/{Key}", s3Ref.Bucket, s3Ref.Key);
        await using var fileStream = File.OpenRead(cacheFilePath);
        var putRequest = new PutObjectRequest
        {
            BucketName = s3Ref.Bucket,
            Key = s3Ref.Key,
            InputStream = fileStream,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
        };

        await _localStackS3Client.PutObjectAsync(putRequest, cancellationToken);
        _logger.LogInformation("✓ Synced s3://{Bucket}/{Key} from cache ({Size} bytes)",
            s3Ref.Bucket, s3Ref.Key, fileStream.Length);
    }

    private string ResolveCacheRoot(DlqReplayConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.S3CachePath))
        {
            return config.S3CachePath;
        }

        return Path.Combine(config.MessageStoragePath, "s3-cache");
    }

    private string GetCacheFilePath(S3Reference s3Ref)
    {
        var normalizedKey = Uri.UnescapeDataString(s3Ref.Key ?? string.Empty)
            .Replace('\\', '/')
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.Combine(_cacheRoot, s3Ref.Bucket, normalizedKey);
    }

    private List<S3Reference> ExtractS3References(List<SavedMessage> messages)
    {
        var references = new List<S3Reference>();

        foreach (var message in messages)
        {
            try
            {
                // Parse the message body as JSON
                var bodyDoc = JsonDocument.Parse(message.Body);
                
                // Check if it's an S3 event notification
                if (bodyDoc.RootElement.TryGetProperty("Records", out var records))
                {
                    foreach (var record in records.EnumerateArray())
                    {
                        if (record.TryGetProperty("s3", out var s3Element))
                        {
                            var bucket = s3Element.GetProperty("bucket").GetProperty("name").GetString();
                            var key = s3Element.GetProperty("object").GetProperty("key").GetString();

                            if (!string.IsNullOrEmpty(bucket) && !string.IsNullOrEmpty(key))
                            {
                                references.Add(new S3Reference
                                {
                                    Bucket = bucket,
                                    Key = key
                                });
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not a JSON message or not an S3 event - skip it
                continue;
            }
        }

        // Remove duplicates
        return references
            .GroupBy(r => $"{r.Bucket}/{r.Key}")
            .Select(g => g.First())
            .ToList();
    }

    private class S3Reference
    {
        public required string Bucket { get; set; }
        public required string Key { get; set; }
    }
}
