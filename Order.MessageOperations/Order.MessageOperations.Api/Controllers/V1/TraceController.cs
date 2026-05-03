using Order.MessageOperations.Api.Models.Requests;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/trace")]
public class TraceController : ControllerBase
{
    private readonly ITraceService _traceService;

    public TraceController(ITraceService traceService)
    {
        _traceService = traceService;
    }

    /// <summary>
    /// Poll LocalStack S3 until an object matching the key prefix appears, or timeout.
    /// </summary>
    [HttpPost("s3")]
    public async Task<IActionResult> WaitForS3Object(
        [FromBody] WaitForS3ObjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BucketName))
            return BadRequest(new ErrorResponse("BucketName is required"));

        if (string.IsNullOrWhiteSpace(request.KeyPrefix))
            return BadRequest(new ErrorResponse("KeyPrefix is required"));

        var result = await _traceService.WaitForS3ObjectAsync(
            request.BucketName,
            request.KeyPrefix,
            request.TimeoutSeconds,
            request.PollIntervalMs,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Poll a LocalStack SQS queue until a matching message appears, or timeout.
    /// </summary>
    [HttpPost("queue")]
    public async Task<IActionResult> WaitForQueueMessage(
        [FromBody] WaitForQueueMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.QueueName))
            return BadRequest(new ErrorResponse("QueueName is required"));

        var result = await _traceService.WaitForQueueMessageAsync(
            request.QueueName,
            request.BodyContains,
            request.TimeoutSeconds,
            request.PollIntervalMs,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Poll MongoDB until a matching document appears for the given store, or timeout.
    /// </summary>
    [HttpPost("mongo")]
    public async Task<IActionResult> WaitForMongoDocument(
        [FromBody] WaitForMongoDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoreId))
            return BadRequest(new ErrorResponse("StoreId is required"));

        var result = await _traceService.WaitForMongoDocumentAsync(
            request.StoreId,
            request.ProviderOrderId,
            request.CustomerId,
            request.TimeoutSeconds,
            request.PollIntervalMs,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get the approximate message count for all configured LocalStack queues.
    /// </summary>
    [HttpGet("queue-depths")]
    public async Task<IActionResult> GetAllQueueDepths(CancellationToken cancellationToken = default)
    {
        var result = await _traceService.GetAllQueueDepthsAsync(cancellationToken);
        return Ok(result);
    }
}
