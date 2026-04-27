using Asp.Versioning;
using OrderHub.Common.Configuration.Auth;
using OrderHub.Common.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace OrderHub.Api.Controllers.V0;

[ApiController]
[ApiVersion("0.0")]
[Route("api/orders")]
[Authorize(Policy = ApiKeyAuthenticationDefaults.AuthorizationPolicy)]
[Consumes("application/vnd.order.v0+json")]
[Produces("application/vnd.order.v0+json")]
public class OrdersController(IOrderManager manager) : ControllerBase
{
    [HttpDelete("bulk-delete", Name = nameof(BulkDeleteOrders))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkDeleteOrders(
        [FromQuery, Required, MinLength(1)] string storeId,
        [FromBody] List<string> orderIds)
    {
        await manager.BulkDeleteOrdersAsync(storeId, orderIds);

        return NoContent();
    }
}
