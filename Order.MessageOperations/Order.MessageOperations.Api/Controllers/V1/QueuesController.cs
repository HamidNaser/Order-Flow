using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Models.Requests;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/queues")]
public class QueuesController : ControllerBase
{
    private readonly MessageOperationsOptions _options;
    private readonly IQueueReplayService _queueReplayService;

    public QueuesController(
        IOptions<MessageOperationsOptions> options,
        IQueueReplayService queueReplayService)
    {
        _options = options.Value;
        _queueReplayService = queueReplayService;
    }

    [HttpGet]
    public IActionResult GetConfiguredQueues()
    {
        var queues = _options.Queues
            .Select(queue => new QueueConfigDto(
                QueueKey: queue.Key,
                DisplayName: queue.Value.DisplayName,
                LocalStackQueueName: queue.Value.LocalStackQueueName,
                AwsDlqName: queue.Value.AwsDlqName,
                AwsSourceQueueName: queue.Value.AwsSourceQueueName,
                Enabled: queue.Value.Enabled))
            .OrderBy(queue => queue.QueueKey)
            .ToList();

        return Ok(queues);
    }

    /// <summary>
    /// List all queues in LocalStack or AWS.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListQueues(
        [FromQuery] string target = "localstack",
        CancellationToken cancellationToken = default)
    {
        var useLocalStack = !target.Equals("aws", StringComparison.OrdinalIgnoreCase);
        var queues = await _queueReplayService.ListQueuesAsync(useLocalStack, cancellationToken);
        return Ok(queues.OrderBy(queue => queue).ToList());
    }

    /// <summary>
    /// List all queues in LocalStack. Preserved for backward compatibility.
    /// </summary>
    [HttpGet("localstack")]
    public async Task<IActionResult> ListLocalStackQueues(CancellationToken cancellationToken)
    {
        var queues = await _queueReplayService.ListLocalStackQueuesAsync(cancellationToken);
        return Ok(queues.OrderBy(queue => queue).ToList());
    }

    /// <summary>
    /// Get status and attributes for a specific queue in LocalStack or AWS.
    /// </summary>
    [HttpGet("{queueName}/status")]
    public async Task<IActionResult> GetQueueStatus(
        string queueName,
        [FromQuery] string target = "localstack",
        CancellationToken cancellationToken = default)
    {
        var useLocalStack = !target.Equals("aws", StringComparison.OrdinalIgnoreCase);
        var attributes = await _queueReplayService.GetQueueAttributesAsync(queueName, useLocalStack, cancellationToken);
        return Ok(attributes);
    }

    /// <summary>
    /// Peek at messages in a queue in LocalStack or AWS without consuming them.
    /// </summary>
    [HttpGet("{queueName}/messages")]
    public async Task<IActionResult> PeekMessages(
        string queueName,
        [FromQuery] int count = 5,
        [FromQuery] string target = "localstack",
        CancellationToken cancellationToken = default)
    {
        var useLocalStack = !target.Equals("aws", StringComparison.OrdinalIgnoreCase);
        var messages = await _queueReplayService.PeekMessagesAsync(queueName, count, useLocalStack, cancellationToken);
        return Ok(messages.Select(message => new PeekedMessageDto(
            MessageId: message.MessageId,
            Attributes: message.Attributes,
            MessageAttributes: message.MessageAttributes,
            Body: message.Body,
            BodySize: message.Body?.Length ?? 0)));
    }

    /// <summary>
    /// Send a message to a LocalStack queue.
    /// </summary>
    [HttpPost("{queueName}/send")]
    public async Task<IActionResult> SendMessage(
        string queueName,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            return BadRequest(new ErrorResponse("Queue name is required"));
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new ErrorResponse("Message body is required"));
        }

        var messageId = await _queueReplayService.SendMessageToLocalStackAsync(
            queueName,
            request.Body,
            request.MessageAttributes,
            request.MessageGroupId,
            cancellationToken);

        return Ok(new SendMessageResponse(queueName, messageId));
    }

    /// <summary>
    /// Purge all messages from a LocalStack queue.
    /// </summary>
    [HttpPost("{queueName}/purge")]
    public async Task<IActionResult> PurgeQueue(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            return BadRequest(new ErrorResponse("Queue name is required"));
        }

        await _queueReplayService.PurgeLocalStackQueueAsync(queueName, cancellationToken);
        return Ok(new PurgeQueueResponse(queueName, true));
    }

    /// <summary>
    /// Purge all configured LocalStack queues (main + DLQ).
    /// </summary>
    [HttpPost("purge-all")]
    public async Task<IActionResult> PurgeAllQueues(CancellationToken cancellationToken = default)
    {
        var results = await _queueReplayService.PurgeAllConfiguredLocalStackQueuesAsync(cancellationToken);
        var purged = results.Count(r => r.Value);
        var failed = results.Count(r => !r.Value);
        return Ok(new PurgeAllQueuesResponse(purged, failed, results));
    }
}
