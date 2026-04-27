using System.Globalization;
using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts;
using OrderGateway.IntegrationTests.Support;
using OrderGateway.IntegrationTests.TestData;
using Xunit;

namespace OrderGateway.IntegrationTests.OrderGatewayApi.V1.EventHandling;

[Collection("ApiTests")]
public class PublishEventTests(ApiTestsFixture fixture)
{
    [Fact(Skip = "Manual test: do not run in CI/CD pipeline")]
    public async Task PublishOrderEvent_ReturnsMessageId()
    {
        const string customerAddress = "CUST-ORD-78901";

        var orderEvent = new OrderEvent
        {
            Type = "Order",
            SubType = "Outbound order",
            Description = "Test order event",
            CreatedOn = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", TestConfig.StoreId.ToString() },
                { "UserId", "765432112" },
                { "CustomerId", "1234567" },
                { "TrackingRef", "7654321" },
                { "SourceTrackingId", "995432112" },
                { "Classification", "ManualOrder" },
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "OriginalMessage", "Test order event original message content" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderTitle", "Test Order Title" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "RecipientAddress", customerAddress },
                { "OrderFlowType", "outbound" },
                { "HasAttachments", "False" },
                { "OrderFlags", "0" },
                { "OrderTypeId", "3" }
            }
        };
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        var globalCustomerId = await CustomerTestHelper.EnsureCustomerAndGetIdAsync(
            orderEvent.StoreId!.Value,
            orderEvent.UserId!.Value,
            orderAddress: customerAddress,
            lastName: $"OrderEventCustomer{env}"
        );

        orderEvent.Metadata["CustomerId"] = globalCustomerId.ToString();

        var response = await fixture.OrderGatewayApiV1Client.PublishOrderEventAsync(orderEvent);

        Assert.Equal(200, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Result));
    }
}
