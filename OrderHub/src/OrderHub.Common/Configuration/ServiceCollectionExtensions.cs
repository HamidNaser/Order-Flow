using CorrelationId.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureCommon(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddCorrelationIdSupport(configuration)
            .ConfigureCache(configuration)
            .ConfigureLocking(configuration)
            .ConfigureAws(configuration)
            .ConfigureChannels(configuration)
            .ConfigureHandlers(configuration)
            .ConfigureResourceAccess(configuration)
            .ConfigureQueuePublishers(configuration)
            .ConfigureServices()
            .ConfigureManagers()
            .ConfigureHttpClients(configuration);

        return services;
    }

    private static IServiceCollection AddCorrelationIdSupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDefaultCorrelationId(config =>
        {
            config.AddToLoggingScope = true;
            config.LoggingScopeKey = "XOrderCorrelationId";
            config.RequestHeader = "X-Order-Correlation-Id";
        });

        return services;
    }

    private static IServiceCollection ConfigureHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
