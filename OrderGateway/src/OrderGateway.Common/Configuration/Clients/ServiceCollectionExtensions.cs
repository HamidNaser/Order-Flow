using CorrelationId.DependencyInjection;
using OrderGateway.Common.Clients.CloudContent.V1;
using OrderGateway.Common.Clients.IngestStandardApi.V1;
using OrderGateway.Common.Clients.IngestExpressApi.V1;
using OrderGateway.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddCorrelationIdSupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDefaultCorrelationId(config =>
        {
            config.AddToLoggingScope = true;
            config.LoggingScopeKey = "XOrderCorrelationId";
            config.RequestHeader = "X-Order-Correlation-Id";
        });

        return services;
    }

    public static IServiceCollection ConfigureHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .RegisterNSwagOAuthClient<IIngestExpressClient, IngestExpressClient>(configuration)
            .RegisterNSwagOAuthClient<IIngestStandardClient, IngestStandardClient>(configuration)
            .RegisterNSwagOAuthClient<ICloudContentClient, CloudContentClient>(configuration);

        return services;
    }
}
