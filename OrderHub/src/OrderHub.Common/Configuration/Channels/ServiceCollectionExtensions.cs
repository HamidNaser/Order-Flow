using OrderHub.Common.Configuration.Channels;
using OrderHub.Common.Models;
using OrderHub.Common.Models.OrderMappers;
using OrderHub.Common.Models.OrderMappers.EntityMappers;
using OrderHub.Common.Models.OrderMappers.IngestionMappers;
using OrderHub.Common.Models.OrderMappers.ResponseMappers;
using OrderHub.Common.Repositories.Entities;
using OrderHub.Contracts.Access;
using OrderHub.Contracts.Ingest;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureChannels(this IServiceCollection services, IConfiguration configuration)
    {
        var contentPreviewConfig = configuration.Get<OrderSummaryConfig>() ?? new OrderSummaryConfig();
        services.AddSingleton(contentPreviewConfig);

        services.RegisterChannel<ShipmentOrder,
            ShipmentEntity, ShipmentEntityMapper,
            GetShipmentResponse, ShipmentOrderResponseMapper,
            AddShipmentOrderRequest, ShipmentOrderIngestionMapper>();

        services.RegisterChannel<DigitalOrder,
            DigitalEntity, DigitalEntityMapper,
            GetDigitalResponse, DigitalOrderResponseMapper,
            AddDigitalOrderRequest, DigitalOrderIngestionMapper>();

        services.AddSingleton<IOrderMapper, OrderMapper>();

        return services;
    }

    private static void RegisterChannel<TOrder, TEntity, TEntityMapper, TGetResponse, TGetResponseMapper, TRequest, TIngestionMapper>(this IServiceCollection services)
        where TOrder : ChannelOrder
        where TEntity : OrderEntity
        where TEntityMapper : class, IOrderEntityMapper
        where TGetResponse : GetOrderResponse
        where TGetResponseMapper : class, IOrderResponseMapper
        where TRequest : OrderRequest
        where TIngestionMapper : class, IOrderIngestionMapper
    {
        services.AddSingleton<TEntityMapper>();
        services.AddSingleton<IOrderEntityMapper, TEntityMapper>();

        services.AddSingleton<TGetResponseMapper>();
        services.AddSingleton<IOrderResponseMapper, TGetResponseMapper>();

        services.AddSingleton<TIngestionMapper>();
        services.AddSingleton<IOrderIngestionMapper, TIngestionMapper>();

        services.AddSingleton(new OrderChannelTypeRegistration
        {
            OrderType = typeof(TOrder),
            EntityType = typeof(TEntity),
            EntityMapperType = typeof(TEntityMapper),
            GetResponseType = typeof(TGetResponse),
            ResponseMapperType = typeof(TGetResponseMapper),
            RequestType = typeof(TRequest),
            IngestionMapperType = typeof(TIngestionMapper)
        });
    }
}

public class OrderChannelTypeRegistration
{
    public required Type OrderType { get; init; }
    public required Type EntityType { get; init; }
    public required Type EntityMapperType { get; init; }
    public required Type GetResponseType { get; init; }
    public required Type ResponseMapperType { get; init; }
    public required Type RequestType { get; init; }
    public required Type IngestionMapperType { get; init; }
}
