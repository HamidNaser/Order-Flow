using Order.MessageOperations.Api.Models.Requests;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/s3")]
public class S3Controller : ControllerBase
{
    private readonly S3OperationsService _s3OperationsService;
    private readonly MessageStorageService _messageStorageService;

    public S3Controller(
        S3OperationsService s3OperationsService,
        MessageStorageService messageStorageService)
    {
        _s3OperationsService = s3OperationsService;
        _messageStorageService = messageStorageService;
    }

    [HttpGet("buckets")]
    public async Task<IActionResult> ListBuckets(
        [FromQuery] string target = "localstack",
        CancellationToken cancellationToken = default)
    {
        var useLocalStack = !target.Equals("aws", StringComparison.OrdinalIgnoreCase);
        var buckets = await _s3OperationsService.ListBucketsAsync(useLocalStack, cancellationToken);
        return Ok(buckets);
    }

    [HttpGet("buckets/{bucketName}/objects")]
    public async Task<IActionResult> ListObjects(
        string bucketName,
        [FromQuery] string? prefix,
        [FromQuery] int maxKeys = 100,
        [FromQuery] string target = "localstack",
        CancellationToken cancellationToken = default)
    {
        var useLocalStack = !target.Equals("aws", StringComparison.OrdinalIgnoreCase);
        var objects = await _s3OperationsService.ListObjectsAsync(bucketName, prefix, maxKeys, useLocalStack, cancellationToken);
        return Ok(objects);
    }

    [HttpGet("buckets/{bucketName}/objects/metadata")]
    public async Task<IActionResult> GetObjectMetadata(
        string bucketName,
        [FromQuery] string key,
        [FromQuery] string target = "localstack",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { Message = "Query parameter 'key' is required" });
        }

        var useLocalStack = !target.Equals("aws", StringComparison.OrdinalIgnoreCase);
        var metadata = await _s3OperationsService.GetObjectMetadataAsync(bucketName, key, useLocalStack, cancellationToken);
        return Ok(metadata);
    }

    [HttpGet("buckets/{bucketName}/objects/content")]
    public async Task<IActionResult> GetObjectContent(
        string bucketName,
        [FromQuery] string key,
        [FromQuery] int maxBytes = 262144,
        [FromQuery] string target = "localstack",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { Message = "Query parameter 'key' is required" });
        }

        var useLocalStack = !target.Equals("aws", StringComparison.OrdinalIgnoreCase);
        var content = await _s3OperationsService.GetObjectContentAsync(bucketName, key, useLocalStack, maxBytes, cancellationToken);
        return Ok(content);
    }

    [HttpPost("sync-from-batch")]
    public async Task<IActionResult> SyncFromBatch(
        [FromBody] SyncS3FromBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QueueType) || string.IsNullOrWhiteSpace(request.BatchId))
        {
            return BadRequest(new { Message = "QueueType and BatchId are required" });
        }

        var batchPath = _messageStorageService.BuildBatchPath(request.QueueType, request.BatchId);
        if (!Directory.Exists(batchPath))
        {
            return NotFound(new { Message = "Batch not found" });
        }

        var messages = await _messageStorageService.LoadBatchAsync(batchPath);
        var synced = await _s3OperationsService.SyncS3ObjectsForMessagesAsync(
            messages,
            request.UseAwsFallback,
            cancellationToken);

        return Ok(new
        {
            Synced = synced,
            TotalMessages = messages.Count,
            request.UseAwsFallback
        });
    }
}
