namespace OrderGateway.Common.Models;

public class RedisCacheSetRequest
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
