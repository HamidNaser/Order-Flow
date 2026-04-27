using OrderHub.Common.Managers;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection ConfigureManagers(this IServiceCollection services)
    {
        services.AddSingleton<OrderManager>();
        services.AddSingleton<IOrderManager>(sp => new OrderManagerLogDecorator(sp.GetRequiredService<OrderManager>()));
        services.AddSingleton<OrderIngestManager>();
        services.AddSingleton<IOrderIngestManager>(sp => new OrderIngestManagerLogDecorator(sp.GetRequiredService<OrderIngestManager>()));

        return services;
    }
}
