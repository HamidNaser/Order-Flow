using OrderHub.Common.Exceptions;
using OrderHub.Common.Services;
using OrderHub.Common.Utilities;
using OrderHub.Contracts.Access;
using OrderHub.Contracts.Common;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Common.Models.OrderMappers.ResponseMappers;

public class DigitalOrderResponseMapper : IOrderResponseMapper
{
    public GetFullOrderResponse ToFullResponseModel(ChannelOrder order, string? content)
    {
        if (order is not DigitalOrder textOrder)
        {
            throw new InvalidChannelMappingException(
                nameof(DigitalOrderResponseMapper),
                nameof(ToFullResponseModel),
                order.GetType().Name
            );
        }

        return ToFullResponseModel(textOrder, content);
    }

    public GetOrderResponse ToResponseModel(ChannelOrder order)
    {
        if (order is not DigitalOrder textOrder)
        {
            throw new InvalidChannelMappingException(
                nameof(DigitalOrderResponseMapper),
                nameof(ToResponseModel),
                order.GetType().Name
            );
        }

        return ToResponseModel(textOrder);
    }

    private GetDigitalResponse ToResponseModel(DigitalOrder order)
    {
        var s3OrderKey = new S3OrderKey
        {
            Priority = order.Priority,
            MerchantName = order.Merchant.Name,
            ChannelType = ChannelType.DIGITAL,
            SourceOrderId = order.Merchant.OrderId,
            OrderId = order.OrderId!
        }.ToKeyString();

        var result = new GetDigitalResponse
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
            Endpoints = new Contracts.Common.Endpoints
            {
                From = order.Endpoints.From,
                To = order.Endpoints.To
            },
            OrderMetadata = order.OrderMetadata.ToOrderMetadataResponseModel(Base64UrlTextEncoderHelper.Encode(s3OrderKey)),
        };

        return result;
    }

    private GetFullOrderResponse ToFullResponseModel(DigitalOrder order, string? content)
    {
        return new GetFullOrderResponse
        {
            Order = ToResponseModel(order),
            Content = content
        };
    }
}
