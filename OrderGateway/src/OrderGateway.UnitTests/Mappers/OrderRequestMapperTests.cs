using OrderGateway.Common.Models;
using StandardContracts = OrderGateway.Common.Clients.IngestStandardApi.V1;
using ExpressContracts = OrderGateway.Common.Clients.IngestExpressApi.V1;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Services.Mapping;
using Xunit;

namespace OrderGateway.UnitTests.Mappers;

public class OrderRequestMapperTests
{
    private readonly OrderRequestMapper _mapper = new();

    private static OrderEvent CreateOrderEvent(bool outbound = true)
    {
        var now = DateTime.UtcNow.ToString("O");
        var orderRefId = Guid.NewGuid().ToString();
        var recipientAddr = outbound ? "CUST-ORD-78901" : "STORE-AGT-001";
        var senderAddr = outbound ? "STORE-AGT-001" : "STORE-ORD-10001";
        return new OrderEvent
        {
            CreatedOn = now,
            Metadata = new Dictionary<string, string>
            {
                {"RecipientAddress", recipientAddr},
                {"SenderAddress", senderAddr},
                {"OrderTitle", "Test Order Title"},
                {"OrderReferenceId", orderRefId},
                {"TrackingRef", "TRACK-1"},
                {"StoreId", "123"},
                {"CustomerId", "456"},
                {"UserId", "789"},
                // OrderFlowType drives direction logic inside OrderEvent
                {"OrderFlowType", outbound ? "some-outbound" : "inbound-response"},
                {"Classification", "batch"}
            },
            RecipientAddresses = [new ContactAddress(recipientAddr)],
            SenderAddress = new ContactAddress(senderAddr)
        };
    }

    private static StepContext CreateContext() => new()
    {
        MessageContent = "Hello World"
    };

    [Fact]
    public void MapAuto_PopulatesExpectedFields_Outbound()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        var context = CreateContext();
        var expectedOrderRefId = orderEvent.Metadata!["OrderReferenceId"];

        var result = _mapper.MapStandard(orderEvent, context);

        Assert.Equal("CUST-ORD-78901", result.To.Single().Address);
        Assert.Equal("STORE-AGT-001", result.From.Address);
        Assert.Equal("Test Order Title", result.OrderTitle);
        Assert.Equal(context.MessageContent, result.Content);
        Assert.Equal("123", result.StoreId);
        Assert.Equal("456", result.CustomerId);
        Assert.Equal("789", result.AgentId);
        Assert.Equal(StandardContracts.OrderFlowType.OUTGOING, result.OrderFlow);
        Assert.NotEqual(default, result.OrderPlacedDate);
        Assert.Equal(StandardContracts.FulfillmentStatus.IN_PROGRESS, result.FulfillmentStatus);
        Assert.NotNull(result.Merchant);
        Assert.Equal(StandardContracts.MerchantName.PRIME, result.Merchant!.Name);
        Assert.Equal(expectedOrderRefId, result.Merchant.OrderId);
        Assert.NotNull(result.Platform);
        Assert.Equal("123", result.Platform!.OperationId);
        Assert.Equal(StandardContracts.PlatformId.ORDER_DIRECT, result.Platform.Id);
        Assert.Equal("TRACK-1", result.Platform.TrackingId);
        Assert.Equal("789", result.Platform.AgentId);
        Assert.Equal("456", result.Platform.CustomerId);
    }

    [Fact]
    public void MapAuto_PopulatesExpectedFields_Inbound()
    {
        var orderEvent = CreateOrderEvent(outbound: false);
        var context = CreateContext();
        var expectedOrderRefId = orderEvent.Metadata!["OrderReferenceId"];
        var result = _mapper.MapStandard(orderEvent, context);

        Assert.Equal("STORE-AGT-001", result.To.Single().Address); // inbound reverses contact perspective
        Assert.Equal("STORE-ORD-10001", result.From.Address);
        Assert.Equal("Test Order Title", result.OrderTitle);
        Assert.Equal(context.MessageContent, result.Content);
        Assert.Equal("123", result.StoreId);
        Assert.Equal("456", result.CustomerId);
        Assert.Equal("789", result.AgentId);
        Assert.Equal(StandardContracts.OrderFlowType.INCOMING, result.OrderFlow);
        Assert.NotEqual(default, result.OrderPlacedDate);
        Assert.Equal(StandardContracts.FulfillmentStatus.SUCCESS, result.FulfillmentStatus);
        Assert.NotNull(result.Merchant);
        Assert.Equal(StandardContracts.MerchantName.PRIME, result.Merchant!.Name);
        Assert.Equal(expectedOrderRefId, result.Merchant.OrderId);
        Assert.NotNull(result.Platform);
        Assert.Equal("123", result.Platform!.OperationId);
        Assert.Equal(StandardContracts.PlatformId.ORDER_DIRECT, result.Platform.Id);
        Assert.Equal("TRACK-1", result.Platform.TrackingId);
        Assert.Equal("789", result.Platform.AgentId);
        Assert.Equal("456", result.Platform.CustomerId);
    }

    [Fact]
    public void MapTxn_PopulatesExpectedFields_Outbound()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        var context = CreateContext();
        var expectedOrderRefId = orderEvent.Metadata!["OrderReferenceId"];
        var result = _mapper.MapExpress(orderEvent, context);

        Assert.Equal("CUST-ORD-78901", result.To.Single().Address);
        Assert.Equal("STORE-AGT-001", result.From.Address);
        Assert.Equal("Test Order Title", result.OrderTitle);
        Assert.Equal(context.MessageContent, result.Content);
        Assert.Equal("123", result.StoreId);
        Assert.Equal("456", result.CustomerId);
        Assert.Equal("789", result.AgentId);
        Assert.Equal(ExpressContracts.OrderFlowType.OUTGOING, result.OrderFlow);
        Assert.NotEqual(default, result.OrderPlacedDate);
        Assert.Equal(ExpressContracts.FulfillmentStatus.IN_PROGRESS, result.FulfillmentStatus);
        Assert.NotNull(result.Merchant);
        Assert.Equal(ExpressContracts.MerchantName.PRIME, result.Merchant!.Name);
        Assert.Equal(expectedOrderRefId, result.Merchant.OrderId);
        Assert.NotNull(result.Platform);
        Assert.Equal("123", result.Platform!.OperationId);
        Assert.Equal(ExpressContracts.PlatformId.ORDER_DIRECT, result.Platform.Id);
        Assert.Equal("TRACK-1", result.Platform.TrackingId);
        Assert.Equal("789", result.Platform.AgentId);
        Assert.Equal("456", result.Platform.CustomerId);
    }

    [Fact]
    public void MapTxn_PopulatesExpectedFields_Inbound()
    {
        var orderEvent = CreateOrderEvent(outbound: false);
        var context = CreateContext();
        var expectedOrderRefId = orderEvent.Metadata!["OrderReferenceId"];
        var result = _mapper.MapExpress(orderEvent, context);

        Assert.Equal("STORE-AGT-001", result.To.Single().Address);
        Assert.Equal("STORE-ORD-10001", result.From.Address);
        Assert.Equal("Test Order Title", result.OrderTitle);
        Assert.Equal(context.MessageContent, result.Content);
        Assert.Equal("123", result.StoreId);
        Assert.Equal("456", result.CustomerId);
        Assert.Equal("789", result.AgentId);
        Assert.Equal(ExpressContracts.OrderFlowType.INCOMING, result.OrderFlow);
        Assert.NotEqual(default, result.OrderPlacedDate);
        Assert.Equal(ExpressContracts.FulfillmentStatus.SUCCESS, result.FulfillmentStatus);
        Assert.NotNull(result.Merchant);
        Assert.Equal(ExpressContracts.MerchantName.PRIME, result.Merchant!.Name);
        Assert.Equal(expectedOrderRefId, result.Merchant.OrderId);
        Assert.NotNull(result.Platform);
        Assert.Equal("123", result.Platform!.OperationId);
        Assert.Equal(ExpressContracts.PlatformId.ORDER_DIRECT, result.Platform.Id);
        Assert.Equal("TRACK-1", result.Platform.TrackingId);
        Assert.Equal("789", result.Platform.AgentId);
        Assert.Equal("456", result.Platform.CustomerId);
    }

    [Fact]
    public void MapAuto_UsesFallbackDate_WhenCreatedOnInvalid()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.CreatedOn = "not-a-date"; // force fallback
        var context = CreateContext();
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var result = _mapper.MapStandard(orderEvent, context);
        var after = DateTimeOffset.UtcNow.AddSeconds(2);
        Assert.InRange(result.OrderPlacedDate, before, after);
    }

    [Fact]
    public void MapTxn_ParsesExactOrderPlacedDate_WhenValidIso()
    {
        var target = DateTimeOffset.UtcNow.AddMinutes(-15).ToUniversalTime();
        var orderEvent = CreateOrderEvent(outbound: false);
        orderEvent.CreatedOn = target.ToString("O");
        var context = CreateContext();
        var result = _mapper.MapExpress(orderEvent, context);
        Assert.Equal(target.ToUnixTimeSeconds(), result.OrderPlacedDate.ToUnixTimeSeconds());
    }

    [Fact]
    public void MapAuto_DirectionDetection_CaseInsensitiveOutbound()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        // override OrderFlowType with weird casing
        orderEvent.Metadata!["OrderFlowType"] = "OutBound Message";
        var context = CreateContext();
        var result = _mapper.MapStandard(orderEvent, context);
        Assert.Equal(StandardContracts.OrderFlowType.OUTGOING, result.OrderFlow);
    }

    [Fact]
    public void MapTxn_MissingOptionalReferralId_AllowsNullPlatformTrackingId()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.Metadata!.Remove("TrackingRef");
        var context = CreateContext();
        var result = _mapper.MapExpress(orderEvent, context);
        Assert.Null(result.Platform!.TrackingId);
        // other core fields still mapped
        Assert.NotNull(result.Merchant?.OrderId);
    }

    [Fact]
    public void MapAuto_ParsesOffsetDate_PreservesInstant()
    {
        var offset = new DateTimeOffset(2025, 9, 23, 8, 15, 0, TimeSpan.FromHours(-5)); // 08:15 -0500
        var expectedUtc = offset.ToUniversalTime();
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.CreatedOn = offset.ToString("O");
        var ctx = CreateContext();
        var result = _mapper.MapStandard(orderEvent, ctx);
        Assert.Equal(expectedUtc.ToUnixTimeSeconds(), result.OrderPlacedDate.ToUnixTimeSeconds());
    }

    [Fact]
    public void MapTxn_ParsesNaiveDate_AssumesLocalThenConvertsToUtc()
    {
        var naive = new DateTime(2025, 9, 23, 14, 30, 0, DateTimeKind.Unspecified);
        var orderEvent = CreateOrderEvent(outbound: false);
        orderEvent.CreatedOn = naive.ToString("yyyy-MM-dd'T'HH:mm:ss");
        var ctx = CreateContext();
        var result = _mapper.MapExpress(orderEvent, ctx);
        // Parse using DateTimeOffset without explicit kind; this mimics mapper's DateTimeOffset.TryParse behavior
        DateTimeOffset.TryParse(orderEvent.CreatedOn, out var parsed);
        // Ensure we matched the same instant as parsed (within 1s tolerance for minor clock differences)
        var diff = (result.OrderPlacedDate - parsed.ToUniversalTime()).Duration();
        Assert.True(diff < TimeSpan.FromSeconds(1), $"Expected ~{parsed.ToUniversalTime():o} got {result.OrderPlacedDate:o}");
    }

    [Fact]
    public void MapAuto_FallbacksDate_WhenParsedBeforeUnixEpoch()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.CreatedOn = new DateTimeOffset(1950, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("O");
        var context = CreateContext();
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var result = _mapper.MapStandard(orderEvent, context);
        var after = DateTimeOffset.UtcNow.AddSeconds(2);
        Assert.InRange(result.OrderPlacedDate, before, after);
    }

    [Fact]
    public void MapTxn_FallbacksDate_WhenParsedBeforeUnixEpoch()
    {
        var orderEvent = CreateOrderEvent(outbound: false);
        orderEvent.CreatedOn = new DateTimeOffset(1960, 6, 15, 0, 0, 0, TimeSpan.Zero).ToString("O");
        var context = CreateContext();
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var result = _mapper.MapExpress(orderEvent, context);
        var after = DateTimeOffset.UtcNow.AddSeconds(2);
        Assert.InRange(result.OrderPlacedDate, before, after);
    }

    [Fact]
    public void MapAuto_MultipleRecipients_MapsAllToAddresses()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-001"),
            new ContactAddress("CUST-ORD-002"),
            new ContactAddress("CUST-ORD-003")
        ];
        var context = CreateContext();
        var result = _mapper.MapStandard(orderEvent, context);

        Assert.Equal(3, result.To.Count);
        var toList = result.To.ToList();
        Assert.Equal("CUST-ORD-001", toList[0].Address);
        Assert.Equal("CUST-ORD-002", toList[1].Address);
        Assert.Equal("CUST-ORD-003", toList[2].Address);
    }

    [Fact]
    public void MapTxn_MultipleRecipients_MapsAllToAddresses()
    {
        var orderEvent = CreateOrderEvent(outbound: false);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-001"),
            new ContactAddress("CUST-ORD-002"),
            new ContactAddress("CUST-ORD-003")
        ];
        var context = CreateContext();
        var result = _mapper.MapExpress(orderEvent, context);

        Assert.Equal(3, result.To.Count);
        var toList = result.To.ToList();
        Assert.Equal("CUST-ORD-001", toList[0].Address);
        Assert.Equal("CUST-ORD-002", toList[1].Address);
        Assert.Equal("CUST-ORD-003", toList[2].Address);
    }

    [Fact]
    public void MapAuto_DisplayName_WhenPresent_MapsToDisplayNameField()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-78901", "Test User")
        ];
        orderEvent.SenderAddress = new ContactAddress("STORE-AGT-001", "Agent Name");
        var context = CreateContext();
        var result = _mapper.MapStandard(orderEvent, context);

        Assert.Equal("Test User", result.To.Single().Name);
        Assert.Equal("Agent Name", result.From.Name);
    }

    [Fact]
    public void MapTxn_DisplayName_WhenPresent_MapsToNameField()
    {
        var orderEvent = CreateOrderEvent(outbound: false);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-78901", "Test User")
        ];
        orderEvent.SenderAddress = new ContactAddress("STORE-AGT-001", "Agent Name");
        var context = CreateContext();
        var result = _mapper.MapExpress(orderEvent, context);

        Assert.Equal("Test User", result.To.Single().Name);
        Assert.Equal("Agent Name", result.From.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MapAuto_DisplayName_WhenNullOrEmpty_MapsToNull(string? displayName)
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-78901", string.IsNullOrEmpty(displayName) ? null : displayName)
        ];
        orderEvent.SenderAddress = new ContactAddress("STORE-AGT-001", string.IsNullOrEmpty(displayName) ? null : displayName);
        var context = CreateContext();
        var result = _mapper.MapStandard(orderEvent, context);

        Assert.Null(result.To.Single().Name);
        Assert.Null(result.From.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MapTxn_DisplayName_WhenNullOrEmpty_MapsToNull(string? displayName)
    {
        var orderEvent = CreateOrderEvent(outbound: false);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-78901", string.IsNullOrEmpty(displayName) ? null : displayName)
        ];
        orderEvent.SenderAddress = new ContactAddress("STORE-AGT-001", string.IsNullOrEmpty(displayName) ? null : displayName);
        var context = CreateContext();
        var result = _mapper.MapExpress(orderEvent, context);

        Assert.Null(result.To.Single().Name);
        Assert.Null(result.From.Name);
    }

    [Fact]
    public void MapAuto_MixedDisplayNames_MapsCorrectly()
    {
        var orderEvent = CreateOrderEvent(outbound: true);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-001", "User One"),
            new ContactAddress("CUST-ORD-002"),
            new ContactAddress("CUST-ORD-003")
        ];
        orderEvent.SenderAddress = new ContactAddress("STORE-AGT-001", "Agent Display");
        var context = CreateContext();
        var result = _mapper.MapStandard(orderEvent, context);

        Assert.Equal(3, result.To.Count);
        var toList = result.To.ToList();
        Assert.Equal("User One", toList[0].Name);
        Assert.Null(toList[1].Name);
        Assert.Null(toList[2].Name);
        Assert.Equal("Agent Display", result.From.Name);
    }

    [Fact]
    public void MapTxn_MixedDisplayNames_MapsCorrectly()
    {
        var orderEvent = CreateOrderEvent(outbound: false);
        orderEvent.RecipientAddresses =
        [
            new ContactAddress("CUST-ORD-001", "User One"),
            new ContactAddress("CUST-ORD-002"),
            new ContactAddress("CUST-ORD-003")
        ];
        orderEvent.SenderAddress = new ContactAddress("STORE-AGT-001", "Agent Display");
        var context = CreateContext();
        var result = _mapper.MapExpress(orderEvent, context);

        Assert.Equal(3, result.To.Count);
        var toList = result.To.ToList();
        Assert.Equal("User One", toList[0].Name);
        Assert.Null(toList[1].Name);
        Assert.Null(toList[2].Name);
        Assert.Equal("Agent Display", result.From.Name);
    }

    [Theory]
    [InlineData(false, true)]  // inbound -> SUCCESS
    [InlineData(true, false)]  // outbound -> IN_PROGRESS
    public void MapAuto_SetsFulfillmentStatus_BasedOnDirection(bool outbound, bool expectSuccess)
    {
        var orderEvent = CreateOrderEvent(outbound);
        var context = CreateContext();
        var result = _mapper.MapStandard(orderEvent, context);

        var expectedStatus = expectSuccess ? StandardContracts.FulfillmentStatus.SUCCESS : StandardContracts.FulfillmentStatus.IN_PROGRESS;
        var expectedOrderFlow = outbound ? StandardContracts.OrderFlowType.OUTGOING : StandardContracts.OrderFlowType.INCOMING;
        Assert.Equal(expectedStatus, result.FulfillmentStatus);
        Assert.Equal(expectedOrderFlow, result.OrderFlow);
    }

    [Theory]
    [InlineData(false, true)]  // inbound -> SUCCESS
    [InlineData(true, false)]  // outbound -> IN_PROGRESS
    public void MapTxn_SetsFulfillmentStatus_BasedOnDirection(bool outbound, bool expectSuccess)
    {
        var orderEvent = CreateOrderEvent(outbound);
        var context = CreateContext();
        var result = _mapper.MapExpress(orderEvent, context);

        var expectedStatus = expectSuccess ? ExpressContracts.FulfillmentStatus.SUCCESS : ExpressContracts.FulfillmentStatus.IN_PROGRESS;
        var expectedOrderFlow = outbound ? ExpressContracts.OrderFlowType.OUTGOING : ExpressContracts.OrderFlowType.INCOMING;
        Assert.Equal(expectedStatus, result.FulfillmentStatus);
        Assert.Equal(expectedOrderFlow, result.OrderFlow);
    }
}
