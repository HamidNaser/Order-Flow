using OrderGateway.Common.Services;
using OrderGateway.Common.Services.Mapping;
using OrderGateway.Common.Telemetry;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IContentSizeMetricEmitter, ContentSizeMetricEmitter>();

        // Use local stub when LocalCloudContent config is present (localstack/local environments);
        // otherwise use the real CloudContent client.
        var localCloudContent = configuration.GetSection("LocalCloudContent");
        if (localCloudContent.Exists() && localCloudContent.GetChildren().Any())
        {
            services.AddSingleton<ICloudContentService>(sp =>
                new LocalCloudContentService(configuration));
        }
        else
        {
            services.AddSingleton<ICloudContentService, CloudContentService>();
        }

        // Request mappers
        services.AddSingleton<IOrderRequestMapper, OrderRequestMapper>();

        // Order service for sending events to ingest APIs
        services.AddSingleton<IOrderService, OrderService>();

        return services;
    }
}
