using System.Text.Json;
using Asp.Versioning;
using Order.MessagePump.Publishers;
using OrderGateway.Common.Configuration.Auth;
using OrderGateway.Common.Configuration.Queues;
using OrderGateway.Common.Models.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace OrderGateway.Api.Controllers.V1.EventHandling;

[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/publish-event")]
[Authorize(Policy = ApiKeyAuthenticationDefaults.AuthorizationPolicy)]
public class PublishEventController(
    Dictionary<SupportedQueues, IPublisherClient> publishers
) : ControllerBase
{
    [HttpPost("standard", Name = nameof(PublishOrderEvent))]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishOrderEvent([FromBody] OrderEvent orderEvent)
    {
        var orderEventString = JsonSerializer.Serialize(orderEvent);
        var orderEventMessage = Base64Encode(orderEventString);

        string messageId = await publishers[SupportedQueues.IncomingOrders].PublishMessageAsync(orderEventMessage);

        Log.ForContext<PublishEventController>().Information("Sent test order message to SQS: {MessageId}", messageId);

        return Ok(messageId);
    }

    private static string Base64Encode(string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }
}
