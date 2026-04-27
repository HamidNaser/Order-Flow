using OrderHub.Common.Exceptions;
using OrderHub.Common.Models.OrderMappers.EntityMappers;
using OrderHub.Common.Models.OrderMappers.IngestionMappers;
using OrderHub.Common.Models.OrderMappers.ResponseMappers;
using OrderHub.Common.Models.Components;
using OrderHub.Common.Repositories.Entities;
using OrderHub.Common.Services;
using OrderHub.Contracts.Access;
using OrderHub.Contracts.Ingest;
using Microsoft.Extensions.DependencyInjection;

namespace OrderHub.Common.Models.OrderMappers;

public class OrderMapper : IOrderMapper
{
    private readonly Dictionary<Type, IOrderResponseMapper> _responseMappersByType;
    private readonly Dictionary<Type, IOrderEntityMapper> _entityMappersByType;
    private readonly Dictionary<Type, IOrderIngestionMapper> _ingestionMappersByType;

    public OrderMapper(IServiceProvider serviceProvider, IEnumerable<OrderChannelTypeRegistration> channelRegistrations)
    {
        _responseMappersByType = new Dictionary<Type, IOrderResponseMapper>();
        _entityMappersByType = new Dictionary<Type, IOrderEntityMapper>();
        _ingestionMappersByType = new Dictionary<Type, IOrderIngestionMapper>();

        foreach (var registration in channelRegistrations)
        {
            var responseMapper = (IOrderResponseMapper)serviceProvider.GetRequiredService(registration.ResponseMapperType);
            var entityMapper = (IOrderEntityMapper)serviceProvider.GetRequiredService(registration.EntityMapperType);
            var ingestionMapper = (IOrderIngestionMapper)serviceProvider.GetRequiredService(registration.IngestionMapperType);

            _responseMappersByType.Add(registration.OrderType, responseMapper);
            _responseMappersByType.Add(registration.GetResponseType, responseMapper);

            _entityMappersByType.Add(registration.OrderType, entityMapper);
            _entityMappersByType.Add(registration.EntityType, entityMapper);

            _ingestionMappersByType.Add(registration.RequestType, ingestionMapper);
        }
    }

    public GetOrderResponse ToResponseModel(ChannelOrder order)
        => GetResponseMapper(order).ToResponseModel(order);

    public GetFullOrderResponse ToFullResponseModel(ChannelOrder order, string? content)
        => GetResponseMapper(order).ToFullResponseModel(order, content);

    public ChannelOrder ToInternalModel(OrderEntity entity)
        => GetEntityMapper(entity).ToInternalModel(entity);

    public OrderEntity ToEntity(ChannelOrder order)
        => GetEntityMapper(order).ToEntity(order);

    public ChannelOrder ToInternalModel(
        OrderRequest request,
        string orderId,
        ContentProcessingResult contentProcessingResult,
        Priority priority
    ) => GetIngestionMapper(request).ToInternalModel(request, orderId, contentProcessingResult, priority);

    public IOrderResponseMapper GetResponseMapper(ChannelOrder order)
        => GetResponseMapperByType(order.GetType());

    public IOrderEntityMapper GetEntityMapper(ChannelOrder order)
        => GetEntityMapperByType(order.GetType());

    public IOrderEntityMapper GetEntityMapper(OrderEntity entity)
        => GetEntityMapperByType(entity.GetType());

    public IOrderIngestionMapper GetIngestionMapper(OrderRequest request)
        => GetIngestionMapperByType(request.GetType());

    private IOrderResponseMapper GetResponseMapperByType(Type type)
    {
        if (_responseMappersByType.TryGetValue(type, out var mapper))
        {
            return mapper;
        }

        throw new UnregisteredChannelTypeException(type.Name);
    }

    private IOrderEntityMapper GetEntityMapperByType(Type type)
    {
        if (_entityMappersByType.TryGetValue(type, out var mapper))
        {
            return mapper;
        }

        throw new UnregisteredChannelTypeException(type.Name);
    }

    private IOrderIngestionMapper GetIngestionMapperByType(Type type)
    {
        if (_ingestionMappersByType.TryGetValue(type, out var mapper))
        {
            return mapper;
        }

        throw new UnregisteredChannelTypeException(type.Name);
    }
}
