using OrderHub.Common.Models.Components;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;

namespace OrderHub.Common.Models.OrderMappers.IngestionMappers;

public class ShipmentOrderIngestionMapper : IOrderIngestionMapper
{
    public ChannelOrder ToInternalModel(
        OrderRequest request,
        string orderId,
        ContentProcessingResult contentProcessingResult,
        Priority priority
    )
    {
        if (request is not AddShipmentOrderRequest orderRequest)
        {
            throw new ArgumentException($"Expected {nameof(AddShipmentOrderRequest)}, but received {request.GetType().Name}", nameof(request));
        }

        var nowTime = DateTimeOffset.UtcNow;

        var shipmentOrder = new ShipmentOrder
        {
            OrderId = orderId,
            TenantId = orderRequest.TenantId,
            CustomerId = orderRequest.CustomerId,
            CustomerName = orderRequest.CustomerName,
            AgentId = orderRequest.AgentId,
            AgentName = orderRequest.AgentName,
            StoreId = orderRequest.StoreId,
            OrderSummary = contentProcessingResult.OrderSummary,
            OrderPlacedDate = orderRequest.OrderPlacedDate,
            OrderFulfilledDate = orderRequest.OrderFulfilledDate,
            OrderFlow = (OrderFlowType)orderRequest.OrderFlow,
            Merchant = orderRequest.Merchant.ToMerchantInternalModel(),
            Platform = orderRequest.Platform.ToPlatformInternalModel(),
            FulfillmentStatus = (FulfillmentStatus)orderRequest.FulfillmentStatus,
            Priority = priority,
            CreatedDate = nowTime,
            UpdatedDate = nowTime,
            To = orderRequest.To
                .Select(r => new AddressInfo
                    {
                        Address = r.Address,
                        Name = r.Name
                    }
                )
                .ToList(),
            From = new AddressInfo
            {
                Address = orderRequest.From.Address,
                Name = orderRequest.From.Name
            },
            OrderTitle = orderRequest.OrderTitle,
            OrderMetadata = new OrderMetadata
            {
                MediaIds = orderRequest.MediaIds ?? [],
                ContentLength = contentProcessingResult.ContentLength,
                VisibleContentLength = contentProcessingResult.VisibleContentLength,
                PlainTextContentLength = contentProcessingResult.PlainTextContentLength
            }
        };

        return shipmentOrder;
    }
}
