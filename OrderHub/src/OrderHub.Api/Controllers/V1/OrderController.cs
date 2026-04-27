using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using OrderHub.Common.Configuration.Auth;
using OrderHub.Common.Managers;
using OrderHub.Common.Models.OrderMappers;
using OrderHub.Contracts;
using OrderHub.Contracts.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderHub.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/orders")]
[Authorize(Policy = BridgeOAuthSettings.ReadOrdersPolicy)]
[Consumes("application/vnd.order.v1+json")]
[Produces("application/vnd.order.v1+json")]
public class OrderController(
    IOrderManager orderManager,
    IOrderMapper orderMapper) : ControllerBase
{
    /// <summary>Get a full order by id.</summary>
    /// <remarks>GET an order (with full content) by order id AND store id.</remarks>
    [HttpGet("id/{orderId}", Name = nameof(GetFullOrder))]
    [ProducesResponseType(typeof(GetFullOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullOrder(string orderId, [FromQuery, Required] string storeId)
    {
        var (order, content) = await orderManager.GetFullOrderByIdAsync(
            storeId,
            orderId
        );

        if (order == null)
        {
            return NotFound();
        }

        var response = orderMapper.ToFullResponseModel(order, content);

        return Ok(response);
    }

    [HttpGet(Name = nameof(GetOrders))]
    [ProducesResponseType(typeof(PaginatedResponse<GetOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrders(
        [FromQuery, Required] string storeId,
        [FromQuery, Required] string customerId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 500)] int pageSize = 25)
    {
        var (count, results) = await orderManager.ReadCustomerOrdersAsync(
            storeId,
            customerId,
            page,
            pageSize
        );

        var urlBase = $"{Request.Scheme}://{Request.Host}{Request.Path}" +
                      $"?{nameof(storeId)}={Uri.EscapeDataString(storeId)}" +
                      $"&{nameof(customerId)}={Uri.EscapeDataString(customerId)}";

        var responseItems = results
            .Select(order => orderMapper.ToResponseModel(order))
            .ToList();

        var response = new PaginatedResponse<GetOrderResponse>(
            urlBase,
            page,
            pageSize,
            count,
            responseItems
        );

        return Ok(response);
    }

    /// <summary>Get order content by encoded S3 key.</summary>
    [HttpGet("content/{key}", Name = nameof(GetOrderContent))]
    [ProducesResponseType(typeof(GetOrderFullContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderContent([Required] string key)
    {
        var content = await orderManager.GetOrderContentByEncodedKeyAsync(key);

        if (content == null) return NotFound();
        var response = new GetOrderFullContentResponse { Content = content };
        return Ok(response);
    }
}
