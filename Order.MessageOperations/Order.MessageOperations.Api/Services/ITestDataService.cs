using Order.MessageOperations.Api.Models.Responses;

namespace Order.MessageOperations.Api.Services;

public interface ITestDataService
{
    /// <summary>
    /// Generate test order payloads ready to send to SQS queues.
    /// Returns orders in OrderGateway event format (for order-gateway-incoming queue)
    /// or OrderHub ingest format (for direct API calls to IngestStandard/IngestExpress).
    /// </summary>
    List<GeneratedOrder> GenerateOrders(
        string priority = "standard",
        string channelType = "STANDARD",
        int count = 1,
        string? storeId = null,
        string format = "gateway");
}
