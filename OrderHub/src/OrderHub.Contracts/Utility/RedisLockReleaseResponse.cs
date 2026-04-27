namespace OrderHub.Contracts.Utility;

public class RedisLockReleaseResponse
{
    public required bool Released { get; init; }
}
