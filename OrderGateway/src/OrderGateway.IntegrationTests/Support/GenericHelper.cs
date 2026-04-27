namespace OrderGateway.IntegrationTests.Support;

public class GenericHelper
{
    private static readonly Random Random = new();
    public static string BuildS3OrderObjectKey(string priority, string provider, string channel, string sourceOrderId, string orderId)
    {
        return $"{priority}/{provider}/{channel}/{sourceOrderId}/{orderId}";
    }

    public static long GetRandomLongId()
    {
        //using really a long number to avoid collision in prod for call and text events for MessageId (a.k.a Provider OrderId).
        return Random.NextInt64(1000000000000000000, long.MaxValue);
    }
}
