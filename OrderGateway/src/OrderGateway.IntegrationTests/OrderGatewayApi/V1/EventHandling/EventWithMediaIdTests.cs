using System.Globalization;
using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts;
using OrderGateway.IntegrationTests.Support;
using OrderGateway.IntegrationTests.TestData;
using Xunit;
using Xunit.Abstractions;
using OrderEvent = OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts.OrderEvent;

namespace OrderGateway.IntegrationTests.OrderGatewayApi.V1.EventHandling;

[Collection("ApiTests")]
public class EventWithMediaIdTests(ApiTestsFixture fixture, ITestOutputHelper output)
{
    private static async Task<OrderEventRequest> CreateBaseOrderEventRequest()
    {
        const string customerAddress = "CUST-ORD-78901";
        const int userId = 765432112;

        var orderEvent = new OrderEvent
        {
            Type = "Order",
            SubType = "Outbound order",
            Description = "Test order event",
            CreatedOn = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", TestConfig.StoreId.ToString() },
                { "UserId", userId.ToString() },
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
            TestConfig.StoreId,
            userId,
            orderAddress: customerAddress,
            lastName: $"OrderEventCustomer{env}"
        );

        orderEvent.Metadata["CustomerId"] = globalCustomerId.ToString();

        return new OrderEventRequest
        {
            Event = orderEvent,
            ApproximateReceiveCount = 1
        };
    }

    [Fact]
    public async Task OrderEvent_Handle_WithVideoMedia_PopulatesMediaIdsInOrderApi()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["VideoMedia"] = "presentation.mp4,demo.avi";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);

        await VerifyMediaIdsInOrderApi(response.Result, expectedMediaIds: ["presentation.mp4", "demo.avi"]);
    }

    [Fact]
    public async Task OrderEvent_Handle_WithCorrelationId_ProcessesSuccessfully()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["CorrelationId"] = $"order-correlation-{Guid.NewGuid()}";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);
    }

    [Fact]
    public async Task OrderEvent_Handle_WithoutVideoMediaOrCorrelationId_ProcessesSuccessfully()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("VideoMedia");
        orderEventRequest.Event.Metadata.Remove("CorrelationId");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);

        var order = await GetOrderFromApi(response.Result);
        Assert.Null(order.OrderMetadata?.MediaIds);
    }

    private static void AssertSuccessfulCompletion(HttpResponse<HandlerResultDto> response)
    {
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(MessageResultAction.Complete, response.Result.Action);
        Assert.True(response.Result.IsSuccess);
        Assert.NotNull(response.Result.StepContext);
        Assert.False(string.IsNullOrWhiteSpace(response.Result.StepContext!.OrderId));
    }

    private async Task<Clients.OrderApi.V1.Contracts.GetOrderResponse> GetOrderFromApi(HandlerResultDto result)
    {
        Assert.NotNull(result.StepContext);
        Assert.False(string.IsNullOrWhiteSpace(result.StepContext!.OrderId));

        var storeId = TestConfig.StoreId.ToString();
        Clients.OrderApi.V1.Contracts.GetOrderResponse? order = null;

        await RetryHelpers.UntilSuccessAsync(async () =>
            {
                var response = await fixture.OrderApiV1Client.GetFullOrderAsync(
                    result.StepContext.OrderId,
                    storeId
                );

                Assert.Equal(200, response.StatusCode);
                Assert.NotNull(response.Result);
                Assert.NotNull(response.Result.Order);

                order = response.Result.Order;
            }
        );

        Assert.NotNull(order);
        return order;
    }

    private async Task VerifyMediaIdsInOrderApi(HandlerResultDto result, string[] expectedMediaIds)
    {
        var order = await GetOrderFromApi(result);

        Assert.NotNull(order.OrderMetadata);
        Assert.NotNull(order.OrderMetadata!.MediaIds);
        Assert.Equal(expectedMediaIds.Length, order.OrderMetadata.MediaIds.Count);

        var actualMediaIds = order.OrderMetadata.MediaIds.ToList();
        for (int i = 0; i < expectedMediaIds.Length; i++)
        {
            Assert.Equal(expectedMediaIds[i], actualMediaIds[i]);
        }
    }
}
