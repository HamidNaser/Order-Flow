using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Models.Requests;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/replay")]
public class ReplayController : ControllerBase
{
    private readonly MessageOperationsOptions _options;
    private readonly IQueueReplayService _queueReplayService;
    private readonly IMessageStorageService _messageStorageService;

    public ReplayController(
        IOptions<MessageOperationsOptions> options,
        IQueueReplayService queueReplayService,
        IMessageStorageService messageStorageService)
    {
        _options = options.Value;
        _queueReplayService = queueReplayService;
        _messageStorageService = messageStorageService;
    }

    [HttpPost("download")]
    public async Task<IActionResult> DownloadMessages(
        [FromBody] DownloadMessagesRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QueueKey))
        {
            return BadRequest(new ErrorResponse("QueueKey is required"));
        }

        if (!_options.Queues.TryGetValue(request.QueueKey, out var queueMapping))
        {
            return NotFound(new ErrorResponse($"Queue key '{request.QueueKey}' not found in configuration"));
        }

        var awsQueueName = !string.IsNullOrWhiteSpace(request.AwsQueueName)
            ? request.AwsQueueName
            : (!string.IsNullOrWhiteSpace(queueMapping.AwsSourceQueueName)
                ? queueMapping.AwsSourceQueueName
                : queueMapping.AwsDlqName);

        var (downloaded, batchPath) = await _queueReplayService.DownloadFromAwsQueueAsyncByName(
            request.QueueKey,
            awsQueueName,
            request.MaxMessages,
            request.MessageId,
            cancellationToken);

        return Ok(new DownloadMessagesResponse(
            Downloaded: downloaded,
            BatchPath: batchPath,
            QueueKey: request.QueueKey,
            AwsQueueName: awsQueueName));
    }

    [HttpPost("from-batch")]
    public async Task<IActionResult> ReplayFromBatch(
        [FromBody] ReplayBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QueueType) || string.IsNullOrWhiteSpace(request.BatchId))
        {
            return BadRequest(new ErrorResponse("QueueType and BatchId are required"));
        }

        var batchPath = _messageStorageService.BuildBatchPath(request.QueueType, request.BatchId);
        if (!Directory.Exists(batchPath))
        {
            return NotFound(new ErrorResponse("Batch not found"));
        }

        var messages = await _messageStorageService.LoadBatchAsync(batchPath);
        if (!messages.Any())
        {
            return Ok(new ReplayFromBatchResponse(Replayed: 0, Total: 0));
        }

        var localStackQueueName = request.LocalStackQueueName;
        if (string.IsNullOrWhiteSpace(localStackQueueName)
            && _options.Queues.TryGetValue(request.QueueType, out var queueMapping))
        {
            localStackQueueName = queueMapping.LocalStackQueueName;
        }

        if (string.IsNullOrWhiteSpace(localStackQueueName))
        {
            return BadRequest(new ErrorResponse("LocalStackQueueName is required when queue mapping is unavailable"));
        }

        var successCount = await _queueReplayService.ReplayToLocalStackAsyncByName(
            localStackQueueName,
            messages,
            cancellationToken);

        return Ok(new ReplayFromBatchResponse(
            Replayed: successCount,
            Total: messages.Count,
            LocalStackQueueName: localStackQueueName));
    }

    [HttpPost("download-and-replay")]
    public async Task<IActionResult> DownloadAndReplay(
        [FromBody] DownloadAndReplayRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QueueKey))
        {
            return BadRequest(new ErrorResponse("QueueKey is required"));
        }

        var replayed = await _queueReplayService.DownloadAndReplayAsync(
            request.QueueKey,
            request.MaxMessages,
            request.MessageId,
            cancellationToken);

        return Ok(new DownloadAndReplayResponse(
            QueueKey: request.QueueKey,
            Replayed: replayed));
    }
}
