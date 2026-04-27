using System.Text.Json;
using Amazon.SQS.Model;
using Asp.Versioning;
using OrderGateway.Common.Configuration;
using OrderGateway.Common.Configuration.Auth;
using OrderGateway.Common.Handlers;
using OrderGateway.Common.Models;
using OrderGateway.Common.Models.Api;
using OrderGateway.Common.Models.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderGateway.Api.Controllers.V1.EventHandling;

/// <summary>
/// Integration test endpoints for processing events through handler pipelines.
/// Not intended for production use - designed for automated integration testing only.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/event-handler")]
[Authorize(Policy = ApiKeyAuthenticationDefaults.AuthorizationPolicy)]
[Produces("application/json")]
[Consumes("application/json")]
public class EventHandlerController(
    OrderEventHandler orderEventHandler
) : ControllerBase
{
    [HttpPost("standard", Name = nameof(HandleOrderEvent))]
    [ProducesResponseType(typeof(HandlerResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> HandleOrderEvent([FromBody] OrderEventRequest request)
    {
        var message = CreateBase64Message(request.Event, request.ApproximateReceiveCount);
        var messageResult = await orderEventHandler.HandleMessageAsync(message);
        return Ok(messageResult.ToDto());
    }

    /// <summary>
    /// Creates an AWS SQS Message with Base64-encoded JSON body for order events.
    /// </summary>
    private static Message CreateBase64Message<T>(T eventData, int approximateReceiveCount)
    {
        var jsonString = JsonSerializer.Serialize(eventData, SerializationConfig.DefaultSettings);
        var base64Body = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonString));

        return new Message
        {
            MessageId = $"test-message-{Guid.NewGuid()}",
            Body = base64Body,
            Attributes = new Dictionary<string, string>
            {
                { "ApproximateReceiveCount", approximateReceiveCount.ToString() }
            }
        };
    }
}
