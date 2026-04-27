using OrderHub.Common.Models.Components;
using OrderHub.Common.Repositories.Entities;
using OrderHub.Common.Services;
using OrderHub.Contracts.Access;
using OrderHub.Contracts.Ingest;

namespace OrderHub.Common.Models.OrderMappers;

public interface IOrderMapper
{
    public GetOrderResponse ToResponseModel(ChannelOrder order);
    public GetFullOrderResponse ToFullResponseModel(ChannelOrder order, string? content);
    public ChannelOrder ToInternalModel(OrderEntity entity);
    public OrderEntity ToEntity(ChannelOrder order);

    public ChannelOrder ToInternalModel(
        OrderRequest request,
        string orderId,
        ContentProcessingResult contentProcessingResult,
        Priority priority
    );
}
