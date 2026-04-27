using OrderHub.Common.Exceptions;
using OrderHub.Common.Models.Components;
using OrderHub.Common.Repositories.Entities;
using MongoDB.Bson;

namespace OrderHub.Common.Models.OrderMappers.EntityMappers;

public class DigitalEntityMapper() : IOrderEntityMapper
{
    public ChannelOrder ToInternalModel(OrderEntity entity)
    {
        if (entity is not DigitalEntity textEntity)
        {
            throw new InvalidChannelMappingException(
                nameof(DigitalEntityMapper),
                nameof(ToInternalModel),
                entity.GetType().Name
            );
        }

        return ToInternalModel(textEntity);
    }

    public OrderEntity ToEntity(ChannelOrder order)
    {
        if (order is not DigitalOrder textOrder)
        {
            throw new InvalidChannelMappingException(
                nameof(DigitalEntityMapper),
                nameof(ToEntity),
                order.GetType().Name
            );
        }

        return ToEntity(textOrder);
    }

    private DigitalOrder ToInternalModel(DigitalEntity entity)
    {
        var isValidOrderFlowType = Enum.TryParse(typeof(OrderFlowType), entity.OrderFlow, out var direction);
        if (!isValidOrderFlowType || direction == null)
        {
            throw new ArgumentException($"Invalid OrderFlowType on entity: {entity.OrderFlow}", nameof(entity));
        }

        var isValidFulfillmentStatus = Enum.TryParse(typeof(FulfillmentStatus), entity.FulfillmentStatus, out var fulfillmentStatus);
        if (!isValidFulfillmentStatus || fulfillmentStatus == null)
        {
            throw new ArgumentException($"Invalid FulfillmentStatus on entity: {entity.FulfillmentStatus}", nameof(entity));
        }

        var isValidPriority = Enum.TryParse(typeof(Priority), entity.Priority, out var priority);
        if (!isValidPriority || priority == null)
        {
            throw new ArgumentException($"Invalid Priority on entity: {entity.Priority}", nameof(entity));
        }

        return new DigitalOrder
        {
            OrderId = entity.OrderId.ToString(),
            CustomerId = entity.CustomerId,
            CustomerName = entity.CustomerName,
            AgentId = entity.AgentId,
            AgentName = entity.AgentName,
            StoreId = entity.StoreId,
            TenantId = entity.TenantId,
            OrderSummary = entity.OrderSummary,
            OrderPlacedDate = entity.OrderPlacedDateUtc,
            OrderFulfilledDate = entity.OrderFulfilledDateUtc,
            OrderFlow = (OrderFlowType)direction,
            Merchant = entity.Merchant.ToMerchantInternalModel(),
            FulfillmentStatus = (FulfillmentStatus)fulfillmentStatus,
            Priority = (Priority)priority,
            Endpoints = entity.Endpoints.ToEndpointsInternalModel(),
            Platform = entity.Platform.ToPlatformInternalModel(),
            OrderMetadata = string.IsNullOrWhiteSpace(entity.OrderSummary) ? null : entity.OrderMetadata.ToOrderMetadataInternalModel(),
            CreatedDate = entity.CreatedDate,
            UpdatedDate = entity.UpdatedDate,
        };
    }

    private DigitalEntity ToEntity(DigitalOrder order)
    {
        return new DigitalEntity
        {
            OrderId = ObjectId.TryParse(order.OrderId, out var objectId) ? objectId : ObjectId.GenerateNewId(),
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            AgentId = order.AgentId,
            AgentName = order.AgentName,
            StoreId = order.StoreId,
            TenantId = order.TenantId,
            OrderSummary = order.OrderSummary,
            OrderPlacedDateUtc = order.OrderPlacedDate.UtcDateTime,
            OrderFulfilledDateUtc = order.OrderFulfilledDate?.UtcDateTime,
            OrderDateUtc = order.GetOrderDateUtc(),
            OrderFlow = order.OrderFlow.ToString(),
            Merchant = order.Merchant.ToMerchantEntity(),
            FulfillmentStatus = order.FulfillmentStatus.ToString(),
            Priority = order.Priority.ToString(),
            Platform = order.Platform.ToPlatformEntity(),
            Endpoints = order.Endpoints.ToEndpointsEntity(),
            OrderMetadata = order.OrderMetadata.ToOrderMetadataEntity(),
            CreatedDate = order.CreatedDate.UtcDateTime,
            UpdatedDate = order.UpdatedDate.UtcDateTime,
        };
    }
}
