using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly IQueueReplayService _queueReplayService;
    private readonly IS3OperationsService _s3OperationsService;
    private readonly MessageOperationsOptions _options;

    public HealthController(
        IQueueReplayService queueReplayService,
        IS3OperationsService s3OperationsService,
        IOptions<MessageOperationsOptions> options)
    {
        _queueReplayService = queueReplayService;
        _s3OperationsService = s3OperationsService;
        _options = options.Value;
    }

    /// <summary>
    /// Check LocalStack health by verifying SQS and S3 connectivity.
    /// </summary>
    [HttpGet("localstack")]
    public async Task<IActionResult> CheckLocalStackHealth(CancellationToken cancellationToken = default)
    {
        var sqsStatus = await CheckSqsHealthAsync(cancellationToken);
        var s3Status = await CheckS3HealthAsync(cancellationToken);

        var healthy = sqsStatus.Healthy && s3Status.Healthy;
        var response = new LocalStackHealthResponse(
            Healthy: healthy,
            Sqs: sqsStatus,
            S3: s3Status,
            LocalStackEndpoint: _options.LocalStackEndpoint);

        return healthy ? Ok(response) : StatusCode(503, response);
    }

    private async Task<LocalStackServiceStatus> CheckSqsHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var queues = await _queueReplayService.ListLocalStackQueuesAsync(cancellationToken);
            return new LocalStackServiceStatus(true, $"{queues.Count} queues found");
        }
        catch (Exception ex)
        {
            return new LocalStackServiceStatus(false, ex.Message);
        }
    }

    private async Task<LocalStackServiceStatus> CheckS3HealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var buckets = await _s3OperationsService.ListBucketsAsync(true, cancellationToken);
            return new LocalStackServiceStatus(true, $"{buckets.Count} buckets found");
        }
        catch (Exception ex)
        {
            return new LocalStackServiceStatus(false, ex.Message);
        }
    }
}
