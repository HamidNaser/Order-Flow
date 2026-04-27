using Asp.Versioning;
using OrderHub.Common.Configuration.Auth;
using OrderHub.Contracts.Utility;
using Order.MessagePump.Locks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace OrderHub.Api.Controllers.V0;

/// <summary>
/// Utility endpoints backed by <see cref="IDistributedCache"/> and customer locks.
/// This controller is primarily intended for local/dev and integration-test workflows.
/// </summary>
[ApiController]
[ApiVersion("0.0")]
[Route("api/redis")]
[Authorize(Policy = ApiKeyAuthenticationDefaults.AuthorizationPolicy)]
[Consumes("application/vnd.order.v0+json")]
[Produces("application/vnd.order.v0+json")]
public class RedisCacheController(IDistributedCache cache, ILockManager lockManager) : ControllerBase
{
    /// <summary>
    /// Reads a string value from the distributed cache by <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Cache key to retrieve.</param>
    /// <returns>A response containing the cached value, or <see langword="null"/> if not found.</returns>
    [HttpGet(Name = $"{nameof(Get)}RedisCache")]
    [ProducesResponseType(typeof(RedisCacheGetResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] string key)
    {
        var value = await cache.GetStringAsync(key);

        return Ok(new RedisCacheGetResponse { Value = value });
    }

    /// <summary>
    /// Writes a string value to the distributed cache.
    /// </summary>
    /// <param name="request">The cache key and value.</param>
    [HttpPost(Name = $"{nameof(Set)}RedisCache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Set([FromBody] RedisCacheSetRequest request)
    {
        await cache.SetStringAsync(request.Key, request.Value);

        return NoContent();
    }

    /// <summary>
    /// Deletes a value from the distributed cache by <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Cache key to delete.</param>
    [HttpDelete(Name = $"{nameof(Delete)}RedisCache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromQuery] string key)
    {
        await cache.RemoveAsync(key);

        return NoContent();
    }

    /// <summary>
    /// Test helper: acquires a customer lock.
    /// Returns the lock fields required to later release the lock via <see cref="ReleaseLock"/>.
    /// </summary>
    /// <remarks>
    /// Uses the same lock mechanism as the message workers (via <see cref="ILockManager"/>).
    /// </remarks>
    /// <param name="request">Customer id to lock, plus an optional TTL.</param>
    /// <returns>Lock receipt information for use when releasing.</returns>
    [HttpPost("locks/acquire", Name = $"{nameof(AcquireLock)}RedisCache")]
    [ProducesResponseType(typeof(RedisLockAcquireResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcquireLock([FromBody] RedisLockAcquireRequest request)
    {
        var ttlSeconds = request.TtlSeconds.GetValueOrDefault(30);
        if (ttlSeconds <= 0)
        {
            ttlSeconds = 30;
        }

        var lockResponse = await lockManager.AcquireLockAsync(new AcquireLockRequest
        {
            LockId = BuildCustomerLockId(request.CustomerId),
            LockDuration = TimeSpan.FromSeconds(ttlSeconds)
        });

        if (lockResponse?.IsLockAcquired != true)
        {
            return Conflict("Lock not acquired");
        }

        var lockData = lockResponse.LockData;
        var lockReceipt = lockData["LockReceipt"]?.ToString() ?? string.Empty;
        var lockId = lockData["LockId"]?.ToString() ?? string.Empty;
        var expiresUtc = (DateTime)lockData["ExpiresUtc"];

        return Ok(new RedisLockAcquireResponse
        {
            LockReceipt = lockReceipt,
            LockId = lockId,
            ExpiresUtc = expiresUtc
        });
    }

    /// <summary>
    /// Test helper: releases a previously acquired lock.
    /// </summary>
    /// <param name="request">Lock fields returned by <see cref="AcquireLock"/>.</param>
    /// <returns>Whether the lock was released.</returns>
    [HttpPost("locks/release", Name = $"{nameof(ReleaseLock)}RedisCache")]
    [ProducesResponseType(typeof(RedisLockReleaseResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReleaseLock([FromBody] RedisLockReleaseRequest request)
    {
        var releaseResult = await lockManager.ReleaseLockAsync(new ReleaseLockRequest
        {
            LockData = new Dictionary<string, object>
            {
                ["LockReceipt"] = request.LockReceipt,
                ["LockId"] = request.LockId
            }
        });

        return Ok(new RedisLockReleaseResponse { Released = releaseResult?.WasReleased == true });
    }

    private static string BuildCustomerLockId(string customerId) => $"ccid:{customerId}";
}
