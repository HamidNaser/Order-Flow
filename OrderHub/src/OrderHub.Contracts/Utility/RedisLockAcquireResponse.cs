namespace OrderHub.Contracts.Utility;

public class RedisLockAcquireResponse
{
    public required string LockReceipt { get; init; }
    public required string LockId { get; init; }
    public required DateTime ExpiresUtc { get; init; }
}
