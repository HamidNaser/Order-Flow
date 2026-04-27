using OrderHub.Contracts.Common;
using OrderHub.Contracts.Common.Enums;
using OrderHub.Contracts.Ingest;

namespace OrderHub.UnitTests.Helpers.TestDataBuilders;

public static class ContractTestDataBuilder
{
    public static AddShipmentOrderRequest CreateDefaultAddShipmentOrderRequest(
        string? tenantId = null,
        string? customerId = null,
        string? customerName = null,
        string? userId = null,
        string? userName = null)
    {
        var request = new AddShipmentOrderRequest
        {
            TenantId = tenantId ?? "tenant101",
            CustomerId = customerId ?? "customer123",
            CustomerName = customerName ?? "Test Customer",
            AgentId = userId ?? "user456",
            AgentName = userName ?? "Test User",
            StoreId = "org789",
            OrderFlow = OrderFlowType.INCOMING,
            Content = "Test shipment content",
            OrderPlacedDate = DateTimeOffset.UtcNow.AddHours(-1),
            OrderFulfilledDate = DateTimeOffset.UtcNow.AddMinutes(1),
            Merchant = CreateDefaultExternalMerchant(),
            FulfillmentStatus = FulfillmentStatus.SUCCESS,
            Platform = CreateDefaultExternalPlatform(),
            To = [ new AddressInfo { Address = "CUST-ORD-78901", Name = "Recipient Name" } ],
            From = new AddressInfo { Address = "STORE-AGT-001", Name = "Sender Name" },
            OrderTitle = "Test Shipment OrderTitle",
        };

        return request;
    }

    public static AddDigitalOrderRequest CreateDefaultAddDigitalOrderRequest(
        string? tenantId = null,
        string? customerId = null,
        string? customerName = null,
        string? userId = null,
        string? userName = null)
    {
        var request = new AddDigitalOrderRequest
        {
            TenantId = tenantId ?? "tenant101",
            CustomerId = customerId ?? "customer123",
            CustomerName = customerName ?? "Test Customer",
            AgentId = userId ?? "user456",
            AgentName = userName ?? "Test User",
            StoreId = "org789",
            OrderFlow = OrderFlowType.OUTGOING,
            Content = "Test digital order content",
            OrderPlacedDate = DateTimeOffset.UtcNow.AddHours(-1),
             OrderFulfilledDate = DateTimeOffset.UtcNow.AddMinutes(1),
            Merchant = CreateDefaultExternalMerchant(),
            FulfillmentStatus = FulfillmentStatus.SUCCESS,
            Platform = CreateDefaultExternalPlatform(),
            ToPhoneNumber = "2234567890",
            FromPhoneNumber = "9987654321",
        };

        return request;
    }

    public static Merchant CreateDefaultExternalMerchant(
        MerchantName name = MerchantName.PRIME,
        string? orderId = null
    ) =>
        new()
        {
            Name = name,
            OrderId = orderId ?? "source123"
        };

    public static Platform CreateDefaultExternalPlatform(
        PlatformId id = PlatformId.ORDER_DIRECT,
        string? operationId = null,
        string? trackingId = null,
        string? customerId = null,
        string? customerName = null,
        string? userId = null,
        string? userName = null
    ) =>
        new()
        {
            Id = id,
            OperationId = operationId ?? "op456",
            TrackingId = trackingId ?? "track789",
            CustomerId = customerId ?? "customer123",
            CustomerName = customerName ?? "Test Customer",
            AgentId = userId ?? "user456",
            AgentName = userName ?? "Test User"
        };
}

