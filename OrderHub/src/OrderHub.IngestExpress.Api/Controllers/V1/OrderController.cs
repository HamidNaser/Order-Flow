using Asp.Versioning;
using OrderHub.Common.Configuration.Auth;
using OrderHub.Common.Managers;
using OrderHub.Common.Models.Components;
using OrderHub.Contracts.Ingest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderHub.IngestExpress.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/order")]
[Authorize(Policy = BridgeOAuthSettings.IngestExpressOrdersPolicy)]
[Consumes("application/vnd.order.v1+json")]
[Produces("application/vnd.order.v1+json")]
public class OrderController(IOrderIngestManager ingest) : ControllerBase
{
    [HttpPost("digital", Name = nameof(AddDigitalOrder))]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(DuplicateOrderResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddDigitalOrder([FromBody] AddDigitalOrderRequest request)
    {
        var response = await ingest.AddOrderAsync(request, Priority.EXPRESS);

        return response.Status switch
        {
            AddOrderResultStatus.NEW_ORDER => Accepted(new OrderResponse
                { Id = response.OrderId }),

            AddOrderResultStatus.DUPLICATE_REQUEST => Conflict(new DuplicateOrderResponse
                { Id = response.OrderId }),

            _ => throw new NotImplementedException()
        };
    }

    [HttpPost("standard", Name = nameof(AddShipmentOrder))]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(DuplicateOrderResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddShipmentOrder([FromBody] AddShipmentOrderRequest request)
    {
        var response = await ingest.AddOrderAsync(request, Priority.EXPRESS);

        return response.Status switch
        {
            AddOrderResultStatus.NEW_ORDER => Accepted(new OrderResponse
                { Id = response.OrderId }),

            AddOrderResultStatus.DUPLICATE_REQUEST => Conflict(new DuplicateOrderResponse
                { Id = response.OrderId }),

            _ => throw new NotImplementedException()
        };
    }

}
