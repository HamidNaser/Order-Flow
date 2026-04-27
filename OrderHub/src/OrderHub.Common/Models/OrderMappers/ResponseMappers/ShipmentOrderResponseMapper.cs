using OrderHub.Common.Exceptions;
using OrderHub.Common.Services;
using OrderHub.Common.Utilities;
using OrderHub.Contracts.Access;
using OrderHub.Contracts.Common;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Common.Models.OrderMappers.ResponseMappers;

public class ShipmentOrderResponseMapper : IOrderResponseMapper
{
    public GetFullOrderResponse ToFullResponseModel(ChannelOrder order, string? content)
    {
        if (order is not ShipmentOrder shipmentOrder)
        {
            throw new InvalidChannelMappingException(
                nameof(ShipmentOrderResponseMapper),
                nameof(ToFullResponseModel),
                order.GetType().Name
            );
        }

        return ToFullResponseModel(shipmentOrder, content);
    }

    public GetOrderResponse ToResponseModel(ChannelOrder order)
    {
        if (order is not ShipmentOrder shipmentOrder)
        {
            throw new InvalidChannelMappingException(
                nameof(ShipmentOrderResponseMapper),
                nameof(ToResponseModel),
                order.GetType().Name
            );
        }

        return ToResponseModel(shipmentOrder);
    }

    private GetShipmentResponse ToResponseModel(ShipmentOrder order)
    {
        var toAddressInfos = order
            .To
            .Select(e => new AddressInfo
                {
                    Address = e.Address,
                    Name = e.Name
                }
            )
            .ToList();

        var s3OrderKey = new S3OrderKey
        {
            Priority = order.Priority,
            MerchantName = order.Merchant.Name,
            ChannelType = ChannelType.STANDARD,
            SourceOrderId = order.Merchant.OrderId,
            OrderId = order.OrderId!
        }.ToKeyString();

        var result = new GetShipmentResponse
        {
            OrderId = order.OrderId!,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            AgentId = order.AgentId,
            AgentName = order.AgentName,
            StoreId = order.StoreId,
            TenantId = order.TenantId,
            OrderSummary = order.OrderSummary,
            OrderPlacedDateUtc = order.OrderPlacedDate,
            OrderFulfilledDateUtc = order.OrderFulfilledDate,
            OrderFlow = (OrderFlowType)order.OrderFlow,
            Merchant = new Merchant
            {
                Name = (MerchantName)order.Merchant.Name,
                OrderId = order.Merchant.OrderId,
                SourceApplication = order.Merchant.SourceApplication
            },
            Platform = order.Platform.ToPlatformResponseModel(),
            FulfillmentStatus = (FulfillmentStatus)order.FulfillmentStatus,
            Priority = (Priority)order.Priority,
            To = toAddressInfos,
            FormattedToRecipients = toAddressInfos.ToFormattedToRecipients(),
            From = new AddressInfo
            {
                Address = order.From.Address,
                Name = order.From.Name
            },
            OrderTitle = order.OrderTitle,
            OrderMetadata = order.OrderMetadata.ToOrderMetadataResponseModel(Base64UrlTextEncoderHelper.Encode(s3OrderKey)),
        };

        return result;
    }

    private GetFullOrderResponse ToFullResponseModel(ShipmentOrder order, string? content)
    {
        return new GetFullOrderResponse
        {
            Order = ToResponseModel(order),
            Content = content
        };
    }
}
