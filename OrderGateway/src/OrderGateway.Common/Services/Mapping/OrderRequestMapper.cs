using StandardContracts = OrderGateway.Common.Clients.IngestStandardApi.V1;
using ExpressContracts = OrderGateway.Common.Clients.IngestExpressApi.V1;
using OrderGateway.Common.Helpers;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;

namespace OrderGateway.Common.Services.Mapping;

public sealed class OrderRequestMapper : IOrderRequestMapper
{
    public StandardContracts.AddShipmentOrderRequest MapStandard(OrderEvent orderEvent, StepContext context)
    {
        return MapCore(
            orderEvent,
            context,
            (address, name) => new StandardContracts.AddressInfo { Address = address, Name = name },
            (direction) => direction == OrderDirection.OUTGOING ? StandardContracts.OrderFlowType.OUTGOING : StandardContracts.OrderFlowType.INCOMING,
            (direction) => direction == OrderDirection.INCOMING ? StandardContracts.FulfillmentStatus.SUCCESS : StandardContracts.FulfillmentStatus.IN_PROGRESS,
            (merchantOrderId) => new StandardContracts.Merchant { Name = StandardContracts.MerchantName.PRIME, OrderId = merchantOrderId },
            (boid, referralId, agentId, customerId) => new StandardContracts.Platform
            {
                Id = StandardContracts.PlatformId.ORDER_DIRECT, OperationId = boid, TrackingId = referralId, AgentId = agentId, CustomerId = customerId
            },
            (to, from, orderTitle, content, storeId, customerId, agentId, orderFlow, fulfillmentStatus, placedDate, fulfilledDate, merchant, platform, mediaIds) =>
                new StandardContracts.AddShipmentOrderRequest
                {
                    To = to, From = from, OrderTitle = orderTitle, Content = content,
                    StoreId = storeId, CustomerId = customerId, AgentId = agentId,
                    OrderFlow = orderFlow, FulfillmentStatus = fulfillmentStatus,
                    OrderPlacedDate = placedDate, OrderFulfilledDate = fulfilledDate,
                    Merchant = merchant, Platform = platform, MediaIds = mediaIds
                }
        );
    }

    public ExpressContracts.AddShipmentOrderRequest MapExpress(OrderEvent orderEvent, StepContext context)
    {
        return MapCore(
            orderEvent,
            context,
            (address, name) => new ExpressContracts.AddressInfo { Address = address, Name = name },
            (direction) => direction == OrderDirection.OUTGOING ? ExpressContracts.OrderFlowType.OUTGOING : ExpressContracts.OrderFlowType.INCOMING,
            (direction) => direction == OrderDirection.INCOMING ? ExpressContracts.FulfillmentStatus.SUCCESS : ExpressContracts.FulfillmentStatus.IN_PROGRESS,
            (merchantOrderId) => new ExpressContracts.Merchant { Name = ExpressContracts.MerchantName.PRIME, OrderId = merchantOrderId },
            (boid, referralId, agentId, customerId) => new ExpressContracts.Platform
            {
                Id = ExpressContracts.PlatformId.ORDER_DIRECT, OperationId = boid, TrackingId = referralId, AgentId = agentId, CustomerId = customerId
            },
            (to, from, orderTitle, content, storeId, customerId, agentId, orderFlow, fulfillmentStatus, placedDate, fulfilledDate, merchant, platform, mediaIds) =>
                new ExpressContracts.AddShipmentOrderRequest
                {
                    To = to, From = from, OrderTitle = orderTitle, Content = content,
                    StoreId = storeId, CustomerId = customerId, AgentId = agentId,
                    OrderFlow = orderFlow, FulfillmentStatus = fulfillmentStatus,
                    OrderPlacedDate = placedDate, OrderFulfilledDate = fulfilledDate,
                    Merchant = merchant, Platform = platform, MediaIds = mediaIds
                }
        );
    }

    private static TRequest MapCore<TRequest, TAddress, TOrderFlow, TFulfillmentStatus, TMerchant, TPlatform>(
        OrderEvent orderEvent,
        StepContext context,
        Func<string, string?, TAddress> createAddress,
        Func<OrderDirection, TOrderFlow> mapOrderFlow,
        Func<OrderDirection, TFulfillmentStatus> mapFulfillmentStatus,
        Func<string, TMerchant> createMerchant,
        Func<string, string?, string?, string, TPlatform> createPlatform,
        Func<List<TAddress>, TAddress, string?, string?, string, string, string?, TOrderFlow, TFulfillmentStatus, DateTimeOffset, DateTimeOffset?, TMerchant, TPlatform, ICollection<string>?, TRequest> createRequest
    )
    {
        var to = orderEvent.RecipientAddresses!
            .Select(ma => createAddress(
                ma.Address,
                string.IsNullOrEmpty(ma.DisplayName) ? null : ma.DisplayName))
            .ToList();

        var from = createAddress(
            orderEvent.SenderAddress!.Address,
            string.IsNullOrEmpty(orderEvent.SenderAddress.DisplayName) ? null : orderEvent.SenderAddress.DisplayName);

        var orderTitle = orderEvent.GetMetadataValue("OrderTitle");
        var storeId = orderEvent.StoreId.ToString();
        var customerId = orderEvent.CustomerId.ToString();
        var agentId = orderEvent.UserId > 0 ? orderEvent.UserId.ToString() : null;
        var placedDate = GetDate(orderEvent.CreatedOn);
        var fulfilledDate = orderEvent.Direction == OrderDirection.INCOMING ? GetDate(orderEvent.CreatedOn) : (DateTimeOffset?)null;
        var merchant = createMerchant(orderEvent.GetMetadataValue("OrderReferenceId")!);
        var platform = createPlatform(storeId, orderEvent.GetMetadataValue("TrackingRef"), agentId, customerId);
        var mediaIds = VideoMediaParser.ParseVideoMediaIds(orderEvent.VideoMedia, "Standard");

        return createRequest(
            to, from, orderTitle, context.MessageContent,
            storeId, customerId, agentId,
            mapOrderFlow(orderEvent.Direction),
            mapFulfillmentStatus(orderEvent.Direction),
            placedDate, fulfilledDate,
            merchant, platform, mediaIds
        );
    }

    private static DateTimeOffset GetDate(string? createdOn)
        => DateTimeOffset.TryParse(createdOn, out var parsed) && parsed > DateTimeOffset.UnixEpoch ? parsed.ToUniversalTime() : DateTimeOffset.UtcNow;
}
