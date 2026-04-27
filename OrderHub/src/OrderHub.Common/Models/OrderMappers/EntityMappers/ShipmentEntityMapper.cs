using OrderHub.Common.Exceptions;
using OrderHub.Common.Models.Components;
using OrderHub.Common.Repositories.Entities;
using MongoDB.Bson;

namespace OrderHub.Common.Models.OrderMappers.EntityMappers;

public class ShipmentEntityMapper : IOrderEntityMapper
{
    public ChannelOrder ToInternalModel(OrderEntity entity)
    {
        if (entity is not ShipmentEntity shipmentEntity)
            throw new InvalidChannelMappingException(nameof(ShipmentEntityMapper), nameof(ToInternalModel), entity.GetType().Name);

        return ToInternalModel(shipmentEntity);
    }

    public OrderEntity ToEntity(ChannelOrder order)
    {
        if (order is not ShipmentOrder shipmentOrder)
            throw new InvalidChannelMappingException(nameof(ShipmentEntityMapper), nameof(ToEntity), order.GetType().Name);

        return ToEntity(shipmentOrder);
    }

    private ShipmentOrder ToInternalModel(ShipmentEntity entity)
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

        return new ShipmentOrder
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
            To = entity.To
                .Select(e => new AddressInfo
                    {
                        Address = e.Address,
                        Name = e.Name
                    }
                )
                .ToList(),
            From = new AddressInfo
            {
                Address = entity.From.Address,
                Name = entity.From.Name
            },
            OrderTitle = entity.OrderTitle,
            Platform = entity.Platform.ToPlatformInternalModel(),
            OrderMetadata = string.IsNullOrWhiteSpace(entity.OrderSummary) ? null : entity.OrderMetadata.ToOrderMetadataInternalModel(),
            CreatedDate = entity.CreatedDate,
            UpdatedDate = entity.UpdatedDate,
        };
    }

    private ShipmentEntity ToEntity(ShipmentOrder order)
    {
        return new ShipmentEntity
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
            To = order.To
                .Select(i => new AddressInfoEntity
                    {
                        Address = i.Address,
                        Name = i.Name
                    }
                )
                .ToList(),
            From = new AddressInfoEntity
            {
                Address = order.From.Address,
                Name = order.From.Name
            },
            OrderTitle = order.OrderTitle,
            Platform = order.Platform.ToPlatformEntity(),
            OrderMetadata = order.OrderMetadata.ToOrderMetadataEntity(),
            CreatedDate = order.CreatedDate.UtcDateTime,
            UpdatedDate = order.UpdatedDate.UtcDateTime,
        };
    }
}
