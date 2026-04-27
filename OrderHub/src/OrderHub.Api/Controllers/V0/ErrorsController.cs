using Asp.Versioning;
using OrderHub.Common.Configuration.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderHub.Api.Controllers.V0;

[ApiController]
[ApiVersion("0.0")]
[Route("api/errors")]
[Authorize(Policy = ApiKeyAuthenticationDefaults.AuthorizationPolicy)]
[Consumes("application/vnd.order.v0+json")]
[Produces("application/vnd.order.v0+json")]
public class ErrorsController : ControllerBase
{
    [HttpGet("unknown", Name = $"{nameof(Unknown)}Error")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Unknown()
    {
        throw new Exception("Unknown Exception Message");
    }
}
