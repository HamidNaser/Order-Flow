using OrderGateway.Common.Managers;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureManagers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IOrderEventManager, OrderEventManager>();
        return services;
    }
}

