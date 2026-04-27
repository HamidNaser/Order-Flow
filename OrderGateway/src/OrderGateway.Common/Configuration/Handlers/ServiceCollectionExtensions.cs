using OrderGateway.Common.Handlers;
using OrderGateway.Common.Configuration.Handlers;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureHandlers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MessageHandlerOptions>()
            .Bind(configuration.GetSection("MessageHandlerOptions"))
            .ValidateOnStart();

        services.AddSingleton<OrderEventHandler>();
        return services;
    }
}

