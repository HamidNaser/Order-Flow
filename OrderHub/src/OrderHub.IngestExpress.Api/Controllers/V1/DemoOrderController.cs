using Asp.Versioning;
using OrderHub.Common.Configuration.Auth;
using OrderHub.Common.FeatureToggle;
using OrderHub.Common.Managers;
using OrderHub.Common.Models.Components;
using OrderHub.Contracts.Ingest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderHub.IngestExpress.Api.Controllers.V1;

/// <summary>Dedicated demo ingest requests for testing and demonstration purposes.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/demo/order")]
[Authorize(Policy = BridgeOAuthSettings.IngestExpressOrdersPolicy)]
[Consumes("application/vnd.order.v1+json")]
[Produces("application/vnd.order.v1+json")]
[Tags("Demo Order")]
public class DemoOrderController(IOrderIngestManager ingest,
    IFeatureToggle featureToggle) : ControllerBase
{
    /// <summary>Add a NEW digital order.</summary>
    /// <remarks>POST a valid digital order request body which responds with an Accepted new OrderId.</remarks>
    /// <response code="202">Accepted - Operation Successful. Order will eventually be accessible.</response>
    [HttpPost("digital", Name = $"Demo{nameof(AddDigitalOrder)}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(DuplicateOrderResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddDigitalOrder([FromBody] AddDigitalOrderRequest request)
    {
        if (!IsCoOrgEnabled(request.StoreId))
        {
            return CreateFeatureNotEnabledProblemDetails(request.StoreId);
        }


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

    /// <summary>Add a NEW standard order.</summary>
    /// <remarks>POST a valid standard order request body which responds with an Accepted new OrderId.</remarks>
    /// <response code="202">Accepted - Operation Successful. Order will eventually be accessible.</response>
    [HttpPost("standard", Name = $"Demo{nameof(AddShipmentOrder)}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(DuplicateOrderResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddShipmentOrder([FromBody] AddShipmentOrderRequest request)
    {
        if (!IsCoOrgEnabled(request.StoreId))
        {
            return CreateFeatureNotEnabledProblemDetails(request.StoreId);
        }

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

    private bool IsCoOrgEnabled(string storeId)
    {
        return featureToggle.IsFeatureEnabled(
            FeatureFlags.OrderApiDemoEnabledFlag,
            new FeatureUser { Key = nameof(DemoOrderController), CommonOrgId = storeId }
        );
    }

    private BadRequestObjectResult CreateFeatureNotEnabledProblemDetails(string storeId)
    {
        var problemDetails = new ValidationProblemDetails
        {
            Title = "Bad Request",
            Detail = $"Not a valid request for organization '{storeId}'.",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1"
        };

        problemDetails.Errors.Add("StoreId", new[] { $"Organization '{storeId}' is not valid for this feature." });

        return BadRequest(problemDetails);
    }
}
