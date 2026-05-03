using OrderHub.Common.Managers;
using OrderHub.Common.Telemetry;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection ConfigureManagers(this IServiceCollection services)
    {
        services.AddSingleton<OrderManager>();
        services.AddSingleton<IOrderManager>(sp => new OrderManagerLogDecorator(sp.GetRequiredService<OrderManager>()));
        services.AddSingleton<OrderIngestManager>();
        services.AddSingleton<IOrderIngestManager>(sp => new OrderIngestManagerLogDecorator(
            sp.GetRequiredService<OrderIngestManager>(),
            sp.GetRequiredService<IOrderMetrics>()));

        return services;
    }
}
