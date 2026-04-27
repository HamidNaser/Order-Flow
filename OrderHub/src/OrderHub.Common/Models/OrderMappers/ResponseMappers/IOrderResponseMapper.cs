using OrderHub.Contracts.Access;

namespace OrderHub.Common.Models.OrderMappers.ResponseMappers;

public interface IOrderResponseMapper
{
    public GetOrderResponse ToResponseModel(ChannelOrder order);
    public GetFullOrderResponse ToFullResponseModel(ChannelOrder order, string? content);
}
