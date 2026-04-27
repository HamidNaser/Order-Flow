namespace OrderHub.Contracts.Utility;

public class RedisLockAcquireRequest
{
    public required string CustomerId { get; set; }

    /// <summary>
    /// Optional TTL for the lock. Defaults to 30 seconds when omitted or invalid.
    /// </summary>
    public int? TtlSeconds { get; set; }
}
