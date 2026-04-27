using Asp.Versioning;
using OrderGateway.Common.Configuration.Auth;
using OrderGateway.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace OrderGateway.Api.Controllers.V1.OrchestrationServices;

[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/redis")]
[Authorize(Policy = ApiKeyAuthenticationDefaults.AuthorizationPolicy)]
[Produces("application/json")]
[Consumes("application/json")]
public class RedisCacheController(IDistributedCache cache) : ControllerBase
{
    [HttpGet(Name = $"{nameof(Get)}RedisCache")]
    [ProducesResponseType(typeof(RedisCacheGetResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] string key)
    {
        var value = await cache.GetStringAsync(key);

        return Ok(new RedisCacheGetResponse { Value = value });
    }

    [HttpPost(Name = $"{nameof(Set)}RedisCache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Set([FromBody] RedisCacheSetRequest request)
    {
        await cache.SetStringAsync(request.Key, request.Value);

        return NoContent();
    }

    [HttpDelete(Name = $"{nameof(Delete)}RedisCache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromQuery] string key)
    {
        await cache.RemoveAsync(key);

        return NoContent();
    }
}
