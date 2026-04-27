using OrderHub.Common.Repositories.Entities;

namespace OrderHub.Common.Models.OrderMappers.EntityMappers;

public interface IOrderEntityMapper
{
    public ChannelOrder ToInternalModel(OrderEntity entity);
    public OrderEntity ToEntity(ChannelOrder order);
}
