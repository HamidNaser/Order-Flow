using OrderHub.Common.Helpers;
using OrderHub.Common.Models.Components;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;

namespace OrderHub.Common.Models.OrderMappers.IngestionMappers;

public class DigitalOrderIngestionMapper : IOrderIngestionMapper
{
    public ChannelOrder ToInternalModel(
        OrderRequest request,
        string orderId,
        ContentProcessingResult contentProcessingResult,
        Priority priority
    )
    {
        if (request is not AddDigitalOrderRequest textRequest)
        {
            throw new ArgumentException($"Expected {nameof(AddDigitalOrderRequest)}, but received {request.GetType().Name}", nameof(request));
        }

        var nowTime = DateTimeOffset.UtcNow;

        var textOrder = new DigitalOrder
        {
            OrderId = orderId,
            TenantId = textRequest.TenantId,
            CustomerId = textRequest.CustomerId,
            CustomerName = textRequest.CustomerName,
            AgentId = textRequest.AgentId,
            AgentName = textRequest.AgentName,
            StoreId = textRequest.StoreId,
            OrderSummary = contentProcessingResult.OrderSummary,
            OrderPlacedDate = textRequest.OrderPlacedDate,
            OrderFulfilledDate = textRequest.OrderFulfilledDate,
            OrderFlow = (OrderFlowType)textRequest.OrderFlow,
            Merchant = textRequest.Merchant.ToMerchantInternalModel(),
            Platform = textRequest.Platform.ToPlatformInternalModel(),
            FulfillmentStatus = (FulfillmentStatus)textRequest.FulfillmentStatus,
            Priority = priority,
            CreatedDate = nowTime,
            UpdatedDate = nowTime,
            Endpoints = new Components.Endpoints
            {
                To = PhoneNumberHelper.Normalize(textRequest.ToPhoneNumber),
                From = PhoneNumberHelper.Normalize(textRequest.FromPhoneNumber),
            },
            OrderMetadata = new OrderMetadata
            {
                MediaIds = textRequest.MediaIds ?? [],
                ContentLength = contentProcessingResult.ContentLength,
                VisibleContentLength = contentProcessingResult.VisibleContentLength,
                PlainTextContentLength = null
            }
        };

        return textOrder;
    }
}
