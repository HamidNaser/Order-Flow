namespace OrderHub.Contracts.Utility;

public class RedisLockReleaseRequest
{
    public required string LockReceipt { get; set; }
    public required string LockId { get; set; }
}
