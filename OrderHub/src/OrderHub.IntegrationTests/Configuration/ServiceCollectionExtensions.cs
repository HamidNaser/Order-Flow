using OrderHub.Common.Configuration.Channels;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterPreviewConfig(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var contentPreviewConfig = configuration.Get<OrderSummaryConfig>() ?? new OrderSummaryConfig();
        services.AddSingleton(contentPreviewConfig);

        return services;
    }
}

