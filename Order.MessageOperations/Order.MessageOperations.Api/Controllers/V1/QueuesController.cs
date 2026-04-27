using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/queues")]
public class QueuesController : ControllerBase
{
    private readonly MessageOperationsOptions _options;
    private readonly QueueReplayService _queueReplayService;

    public QueuesController(
        IOptions<MessageOperationsOptions> options,
        QueueReplayService queueReplayService)
    {
        _options = options.Value;
        _queueReplayService = queueReplayService;
    }

    [HttpGet]
    public IActionResult GetConfiguredQueues()
    {
        var queues = _options.Queues
            .Select(queue => new
            {
                QueueKey = queue.Key,
                queue.Value.DisplayName,
                queue.Value.LocalStackQueueName,
                queue.Value.AwsDlqName,
                queue.Value.AwsSourceQueueName,
                queue.Value.Enabled
            })
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
        return Ok(messages.Select(message => new
        {
            message.MessageId,
            message.Attributes,
            MessageAttributes = message.MessageAttributes,
            Body = message.Body,
            BodySize = message.Body?.Length ?? 0
        }));
    }
}
