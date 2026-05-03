using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Order.MessageOperations.Api.Models.Responses;

namespace Order.MessageOperations.Api.Services;

/// <summary>
/// Generates realistic test order payloads for injection into the order processing pipeline.
/// Supports two output formats:
///   - "gateway": OrderEvent JSON (optionally base64-encoded) for the order-gateway-incoming SQS queue
///   - "ingest": AddShipmentOrderRequest/AddDigitalOrderRequest JSON for direct HTTP POST to IngestStandard/Express APIs
/// </summary>
public class TestDataService : ITestDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] CustomerNames = [
        "John Smith", "Alice Johnson", "Bob Williams", "Carol Davis",
        "Dave Wilson", "Emma Brown", "Frank Miller", "Grace Lee",
        "Henry Taylor", "Irene Anderson"
    ];

    private static readonly string[] AgentNames = [
        "Jane Smith", "Mike Thompson", "Sarah Connor", "Tom Harris",
        "Lisa Chen", "Mark Rodriguez", "Amy Foster", "Chris Baker"
    ];

    private static readonly string[] OrderContents = [
        "Vehicle purchase order confirmed - 2026 Honda Civic EX",
        "Service appointment scheduled - brake inspection and oil change",
        "Parts order submitted - replacement alternator and battery",
        "Trade-in evaluation completed - 2022 Toyota Camry",
        "Finance application approved - 60-month term at 4.9% APR",
        "Extended warranty purchase - 5-year/100K mile coverage",
        "Recall service scheduled - airbag system update",
        "Test drive request confirmed - 2026 Ford Mustang GT"
    ];

    private static int _counter;

    public List<GeneratedOrder> GenerateOrders(
        string priority = "standard",
        string channelType = "STANDARD",
        int count = 1,
        string? storeId = null,
        string format = "gateway")
    {
        var orders = new List<GeneratedOrder>();
        var random = new Random();
        // Default to 10001 — a known feature-flag-enabled store in local config.
        // Random IDs fail the OrderGateway StoreEnabledStep feature flag check.
        var resolvedStoreId = storeId ?? "10001";
        var isExpress = priority.Equals("express", StringComparison.OrdinalIgnoreCase);
        var targetQueue = isExpress ? "order-hub-express-order" : "order-hub-standard-order";

        // If gateway format, the immediate target is the gateway queue
        if (format.Equals("gateway", StringComparison.OrdinalIgnoreCase))
        {
            targetQueue = "order-gateway-incoming";
        }

        for (var i = 0; i < count; i++)
        {
            var seq = Interlocked.Increment(ref _counter);
            var orderId = Guid.NewGuid().ToString();
            var customerId = (1000000 + random.Next(1, 8999999)).ToString();
            var customerName = CustomerNames[random.Next(CustomerNames.Length)];
            var agentName = AgentNames[random.Next(AgentNames.Length)];
            var content = OrderContents[random.Next(OrderContents.Length)];
            var now = DateTimeOffset.UtcNow;

            string body;
            string description;

            if (format.Equals("gateway", StringComparison.OrdinalIgnoreCase))
            {
                // Classification determines priority routing in the gateway:
                // "batch", "scheduled", "deferred" → Standard
                // Anything else (e.g., "ManualOrder") → Express
                var classification = isExpress ? "ManualOrder" : "batch";

                var gatewayEvent = new
                {
                    type = "Order",
                    subType = "Outbound order",
                    description = content,
                    createdOn = now.ToString("O"),
                    metadata = new Dictionary<string, string>
                    {
                        ["StoreId"] = resolvedStoreId,
                        ["UserId"] = (700000000 + random.Next(1, 99999999)).ToString(),
                        ["CustomerId"] = customerId,
                        ["TrackingRef"] = (7000000 + seq).ToString(),
                        ["SourceTrackingId"] = (9900000 + seq).ToString(),
                        ["Classification"] = classification,
                        ["OrderReferenceId"] = orderId,
                        ["OriginalMessage"] = content,
                        ["MessageId"] = Guid.NewGuid().ToString(),
                        ["OrderTitle"] = $"Order #{seq} - {content.Split(" - ").LastOrDefault()?.Trim() ?? "Auto-Generated"}",
                        ["SenderAddress"] = $"STORE-ORD-{resolvedStoreId}",
                        ["RecipientAddress"] = $"CUST-ORD-{customerId}",
                        ["OrderFlowType"] = "outbound",
                        ["HasAttachments"] = "false",
                        ["OrderFlags"] = "0",
                        ["OrderTypeId"] = "3"
                    }
                };

                var json = JsonSerializer.Serialize(gatewayEvent, JsonOptions);

                // Gateway expects base64-encoded message body on the queue
                body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                description = $"[{priority.ToUpper()}] OrderGateway event for store {resolvedStoreId}, customer {customerId}, ref {orderId}";
            }
            else // ingest format
            {
                if (channelType.Equals("DIGITAL", StringComparison.OrdinalIgnoreCase))
                {
                    var ingestRequest = new
                    {
                        channelType = "DIGITAL",
                        storeId = resolvedStoreId,
                        customerId = orderId,
                        customerName,
                        agentId = $"BRIDGE_{seq}",
                        agentName,
                        orderFlow = "INCOMING",
                        content,
                        mediaIds = Array.Empty<string>(),
                        orderPlacedDate = now.ToString("O"),
                        merchant = new
                        {
                            name = "PRIME",
                            orderId = $"e2e-digital-{now:yyyyMMdd}-{seq:D3}",
                            sourceApplication = "Test Generator"
                        },
                        tenantId = $"TENANT-{resolvedStoreId}",
                        fulfillmentStatus = "IN_PROGRESS",
                        toPhoneNumber = $"+1616{3000000 + random.Next(1, 9999999):D7}",
                        fromPhoneNumber = $"+1616{3000000 + random.Next(1, 9999999):D7}"
                    };

                    body = JsonSerializer.Serialize(ingestRequest, JsonOptions);
                    description = $"[{priority.ToUpper()}] Digital order for store {resolvedStoreId}, ref {ingestRequest.merchant.orderId}";
                }
                else // STANDARD (shipment)
                {
                    var ingestRequest = new
                    {
                        channelType = "STANDARD",
                        storeId = resolvedStoreId,
                        customerId = orderId,
                        customerName,
                        agentId = $"BRIDGE_{seq}",
                        agentName,
                        orderFlow = "INCOMING",
                        content = $"<p>{content}</p>",
                        mediaIds = Array.Empty<string>(),
                        orderPlacedDate = now.ToString("O"),
                        orderFulfilledDate = now.AddMinutes(5).ToString("O"),
                        merchant = new
                        {
                            name = "PRIME",
                            orderId = $"e2e-standard-{now:yyyyMMdd}-{seq:D3}",
                            sourceApplication = "Test Generator"
                        },
                        tenantId = $"TENANT-{resolvedStoreId}",
                        fulfillmentStatus = "SUCCESS",
                        platform = new
                        {
                            id = "ORDER_DIRECT",
                            operationId = $"BO_SALES_{resolvedStoreId}",
                            customerId = $"CUST_{random.Next(100000, 999999)}",
                            customerName,
                            agentId = $"SALES_REP_{random.Next(100, 999)}",
                            agentName,
                            trackingId = $"TRACK_{seq:D3}"
                        },
                        to = new[] { new { address = $"ORD-TO-{Guid.NewGuid():N}"[..20], name = customerName } },
                        from = new { address = $"ORD-FROM-{Guid.NewGuid():N}"[..22], name = $"{agentName} (Dealership)" },
                        orderTitle = $"Order #{seq} - Auto-Generated Test"
                    };

                    body = JsonSerializer.Serialize(ingestRequest, JsonOptions);
                    description = $"[{priority.ToUpper()}] Shipment order for store {resolvedStoreId}, ref {ingestRequest.merchant.orderId}";
                }
            }

            orders.Add(new GeneratedOrder(
                Index: i + 1,
                OrderReferenceId: orderId,
                StoreId: resolvedStoreId,
                Priority: isExpress ? "EXPRESS" : "STANDARD",
                ChannelType: channelType.ToUpperInvariant(),
                Format: format.ToLowerInvariant(),
                TargetQueue: targetQueue,
                Body: body,
                Description: description));
        }

        return orders;
    }
}
