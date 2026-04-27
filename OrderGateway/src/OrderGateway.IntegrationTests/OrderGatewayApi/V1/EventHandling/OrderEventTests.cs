using OrderGateway.Common.Helpers;
using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts;
using OrderGateway.IntegrationTests.Support;
using OrderGateway.IntegrationTests.TestData;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;
using OrderApi = OrderGateway.IntegrationTests.Clients.OrderApi.V1.Contracts;

namespace OrderGateway.IntegrationTests.OrderGatewayApi.V1.EventHandling;

[Collection("ApiTests")]
public class OrderEventTests(ApiTestsFixture fixture, ITestOutputHelper output)
{
    private static async Task<OrderEventRequest> CreateBaseOrderEventRequest()
    {
        const string customerAddress = "testcustomer@order-test.com";
        const int userId = 765432112;

        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
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
                { "SenderAddress", "teststore@order-test.com" },
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
    public async Task OrderEvent_Handle_StoreNotEnabled_Returns_CompletedSkipped()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["StoreId"] = "112233";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        AssertFailedCompletion(response);
        Assert.Equal("Store not enabled, skipped.", response.Result.Details);
    }

    [Fact]
    public async Task OrderEvent_Handle_InvalidDirectionOrderFlowType_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("OrderFlowType");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_NoStoreId_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("StoreId");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_NoSenderAddress_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("SenderAddress");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_NoRecipientAddress_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("RecipientAddress");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_NoContactId_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("CustomerId");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_NoOrderReferenceId_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("OrderReferenceId");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_ClassificationNotManual_Returns_Success()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["Classification"] = "NotManualOrder";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_EmptyEvent_FailsValidation()
    {
        var invalidPayload = new OrderEventRequest { Event = new OrderEvent() };

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(invalidPayload);

        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_NoOriginalMessage_WithValidCloudKey_Returns_Success()
    {
        Assert.False(string.IsNullOrWhiteSpace(TestConfig.CloudContentKey), "CloudContentKey configuration value is required for this test.");
        Assert.False(string.IsNullOrWhiteSpace(TestConfig.CloudContentValue), "CloudContentValue configuration value is required for this test.");

        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["MessageCloudContentKey"] = TestConfig.CloudContentKey;
        orderEventRequest.Event.Metadata.Remove("OriginalMessage");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);
        Assert.Equal(TestConfig.CloudContentValue, response.Result.StepContext.MessageContent);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_WithOriginalMessage_SkipsCloudContent_Returns_Success()
    {
        const string originalMessage = "Already here";

        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["OriginalMessage"] = originalMessage;
        orderEventRequest.Event.Metadata.Remove("MessageCloudContentKey");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);
        Assert.Equal(originalMessage, response.Result.StepContext.MessageContent);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_WithSubject_NoOriginalMessage_NoKey_Returns_Success()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("OriginalMessage");
        orderEventRequest.Event.Metadata.Remove("MessageCloudContentKey");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response, hasMessageContent: false);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_WithSubject_NoOriginalMessage_InvalidKey_Returns_Success()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("OriginalMessage");
        orderEventRequest.Event.Metadata["MessageCloudContentKey"] = Guid.NewGuid().ToString("N");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response, hasMessageContent: false);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_NoSubject_NoOriginalMessage_NoKey_Returns_Success()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("OriginalMessage");
        orderEventRequest.Event.Metadata.Remove("MessageCloudContentKey");
        orderEventRequest.Event.Metadata.Remove("OrderTitle");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response, hasMessageContent: false);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_NoSubject_NoOriginalMessage_InvalidKey_Returns_Success()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata.Remove("OriginalMessage");
        orderEventRequest.Event.Metadata["MessageCloudContentKey"] = Guid.NewGuid().ToString("N");
        orderEventRequest.Event.Metadata.Remove("OrderTitle");

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response, hasMessageContent: false);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_ValidAddressFormats_Returns_Success()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["SenderAddress"] = "\"Doug Maxwell\" <admin@order-test.com>";
        orderEventRequest.Event.Metadata["RecipientAddress"] = "\"FirstName, LastName (OrgName - Location)\" <FirstName.LastName@domain.com>";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);
        await AssertOrderApiMatches(response.Result);
    }

    [Fact]
    public async Task OrderEvent_Handle_ValidMultipleRecipientAddresses_Returns_Success()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["SenderAddress"] = "\"Store Agent\" <agent@store.com>";
        orderEventRequest.Event.Metadata["RecipientAddress"] = "user1@test.com, \"Test User Two\" <user2@test.com>, user3@test.com";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        var result = response.Result;

        output.WriteHandlerResult(result);
        AssertSuccessfulCompletion(response);

        await RetryHelpers.UntilSuccessAsync(async () =>
            {
                var storeId = TestConfig.StoreId.ToString();
                var order = await fixture.OrderApiV1Client
                    .GetFullOrderAsync(
                        result.StepContext.OrderId,
                        storeId
                    );

                Assert.Equal(200, order.StatusCode);
                Assert.NotNull(order);
                Assert.NotNull(order.Result);
                Assert.NotNull(order.Result.Order);
                Assert.Equal(result.StepContext.OrderId, order.Result.Order.OrderId);
                Assert.Equal(storeId, order.Result.Order.StoreId);
                Assert.Equal(result.StepContext.MessageContent, order.Result.Content);
                var shipmentResponse = Assert.IsType<OrderApi.GetShipmentResponse>(order.Result.Order);
                Assert.Equal(3, shipmentResponse.To.Count);

                var parsedAddresses = AddressParser.ParseAddressList(
                    "user1@test.com, \"Test User Two\" <user2@test.com>, user3@test.com"
                );

                var expectedToValues = parsedAddresses!.ToList();
                var responseToList = shipmentResponse.To.ToList();
                for (var i = 0; i < expectedToValues.Count; i++)
                {
                    Assert.Equal(expectedToValues[i].Address, responseToList[i].Address);
                    var expectedDisplayName = string.IsNullOrEmpty(expectedToValues[i].DisplayName)
                        ? null
                        : expectedToValues[i].DisplayName;
                    Assert.Equal(expectedDisplayName, responseToList[i].Name);
                }

                var expectedFormatted = string.Join(", ", parsedAddresses!.Select(a =>
                    string.IsNullOrEmpty(a.DisplayName) ? a.Address : $"\"{a.DisplayName}\" <{a.Address}>"));
                Assert.Equal(expectedFormatted, shipmentResponse.FormattedToRecipients);

                Assert.NotNull(shipmentResponse.From);
                Assert.Equal("agent@store.com", shipmentResponse.From.Address);
                Assert.Equal("Store Agent", shipmentResponse.From.Name);
            }
        );
    }

    [Fact]
    public async Task OrderEvent_Handle_InvalidSenderAddress_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["SenderAddress"] = "@domain.com";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_InvalidRecipientAddress_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["RecipientAddress"] = "user@.com";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_InvalidMultiRecipientAddress_Returns_ValidationFailure()
    {
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["RecipientAddress"] = "valid@test.com, @invalid@domain, another@test.com";

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertFailedCompletion(response, "failed validation");
    }

    [Fact]
    public async Task OrderEvent_Handle_IncomingOrder_CreatesSuccessFulfillmentStatus()
    {
        // Order event with OrderFlowType NOT containing "outbound" (FROMCONSUMER direction)
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["OrderFlowType"] = "inbound-customer-reply"; // Maps to FROMCONSUMER

        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);

        // Order API should have record with FulfillmentStatus = SUCCESS and OrderFlow = INCOMING
        await RetryHelpers.UntilSuccessAsync(async () =>
        {
            var storeId = TestConfig.StoreId.ToString();
            var order = await fixture.OrderApiV1Client
                .GetFullOrderAsync(
                    response.Result.StepContext.OrderId,
                    storeId
                );

            Assert.Equal(200, order.StatusCode);
            Assert.NotNull(order.Result?.Order);
            var shipmentResponse = Assert.IsType<OrderApi.GetShipmentResponse>(order.Result.Order);

            Assert.Equal(OrderApi.FulfillmentStatus.SUCCESS, shipmentResponse.FulfillmentStatus);
            Assert.Equal(OrderApi.OrderFlowType.INCOMING, shipmentResponse.OrderFlow);
        });
    }

    [Fact]
    public async Task OrderEvent_Handle_OutgoingOrder_CreatesInProgressFulfillmentStatus()
    {
        // Order event with OrderFlowType containing "outbound" (TOCONSUMER direction)
        var orderEventRequest = await CreateBaseOrderEventRequest();
        orderEventRequest.Event.Metadata["OrderFlowType"] = "outbound-marketing-order"; // Maps to TOCONSUMER

        // WHEN Handler processes the event
        var response = await fixture.OrderGatewayApiV1Client.HandleOrderEventAsync(orderEventRequest);

        output.WriteHandlerResult(response.Result);
        AssertSuccessfulCompletion(response);

        // Order API should have record with FulfillmentStatus = IN_PROGRESS and OrderFlow = OUTGOING
        await RetryHelpers.UntilSuccessAsync(async () =>
        {
            var storeId = TestConfig.StoreId.ToString();
            var order = await fixture.OrderApiV1Client
                .GetFullOrderAsync(
                    response.Result.StepContext.OrderId,
                    storeId
                );

            Assert.Equal(200, order.StatusCode);
            Assert.NotNull(order.Result?.Order);
            var shipmentResponse = Assert.IsType<OrderApi.GetShipmentResponse>(order.Result.Order);

            Assert.Equal(OrderApi.FulfillmentStatus.IN_PROGRESS, shipmentResponse.FulfillmentStatus);
            Assert.Equal(OrderApi.OrderFlowType.OUTGOING, shipmentResponse.OrderFlow);
        });
    }

    private static void AssertSuccessfulCompletion(
        HttpResponse<HandlerResultDto> response,
        bool hasMessageContent = true,
        bool hasOrderId = true
    )
    {
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(MessageResultAction.Complete, response.Result.Action);
        Assert.True(string.IsNullOrWhiteSpace(response.Result.Details));
        Assert.True(response.Result.IsSuccess);
        Assert.Null(response.Result.Backoff);
        Assert.NotNull(response.Result.StepContext);
        Assert.True(string.IsNullOrEmpty(response.Result.ExceptionMessage));

        if (hasMessageContent)
        {
            Assert.False(string.IsNullOrWhiteSpace(response.Result.StepContext!.MessageContent));
        }
        else
        {
            Assert.True(string.IsNullOrEmpty(response.Result.StepContext!.MessageContent));
        }

        if (hasOrderId)
        {
            Assert.False(string.IsNullOrWhiteSpace(response.Result.StepContext.OrderId));
        }
    }

    private static void AssertFailedCompletion(HttpResponse<HandlerResultDto> response, string? expectedDetailsSubstring = null)
    {
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(MessageResultAction.Complete, response.Result.Action);
        Assert.False(response.Result.IsSuccess);
        Assert.Null(response.Result.Backoff);
        Assert.NotNull(response.Result.StepContext);
        Assert.True(string.IsNullOrEmpty(response.Result.ExceptionMessage));

        if (expectedDetailsSubstring != null)
        {
            Assert.Contains(expectedDetailsSubstring, response.Result.Details);
        }
    }

    private static void AssertRetryResponse(HttpResponse<HandlerResultDto> response, TimeSpan expectedBackoff)
    {
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(MessageResultAction.Retry, response.Result.Action);
        Assert.False(response.Result.IsSuccess);
        Assert.True(response.Result.Backoff.HasValue);
        Assert.Equal(expectedBackoff, response.Result.Backoff.Value);
        Assert.NotNull(response.Result.StepContext);
        Assert.True(string.IsNullOrEmpty(response.Result.StepContext!.OrderId));
        Assert.True(string.IsNullOrEmpty(response.Result.ExceptionMessage));
    }

    private static void AssertPoisonResponse(
        HttpResponse<HandlerResultDto> response,
        string? expectedDetailsSubstring = null,
        string? expectedExceptionMessage = null
    )
    {
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(MessageResultAction.Poison, response.Result.Action);
        Assert.False(response.Result.IsSuccess);
        Assert.Null(response.Result.Backoff);

        if (expectedDetailsSubstring != null)
        {
            Assert.Contains(expectedDetailsSubstring, response.Result.Details);
        }

        if (expectedExceptionMessage != null)
        {
            Assert.Equal(expectedExceptionMessage, response.Result.ExceptionMessage);
        }
    }

    private async Task AssertOrderApiMatches(HandlerResultDto result, string? expectedContent = null)
    {
        var storeId = TestConfig.StoreId.ToString();

        await RetryHelpers.UntilSuccessAsync(async () =>
            {
                var order = await fixture.OrderApiV1Client
                    .GetFullOrderAsync(
                        result.StepContext.OrderId,
                        storeId
                    );

                Assert.Equal(200, order.StatusCode);
                Assert.NotNull(order);
                Assert.NotNull(order.Result);
                Assert.NotNull(order.Result.Order);
                Assert.Equal(result.StepContext.OrderId, order.Result.Order.OrderId);
                Assert.Equal(storeId, order.Result.Order.StoreId);
                Assert.Equal(expectedContent ?? result.StepContext.MessageContent, order.Result.Content);
                Assert.IsType<OrderApi.GetShipmentResponse>(order.Result.Order);
            }
        );
    }

    
}

