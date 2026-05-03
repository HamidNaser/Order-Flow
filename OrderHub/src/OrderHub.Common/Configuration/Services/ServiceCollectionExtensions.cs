using OrderHub.Common.Configuration.Clients;
using OrderHub.Common.Models.OrderMappers;
using OrderHub.Common.Services;
using OrderHub.Common.Services.Utils;
using OrderHub.Common.Telemetry;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddSingleton<IOrderMetrics, NewRelicOrderMetrics>();
        services.AddSingleton<IHtmlTextExtractorService, HtmlTextExtractorService>();
        services.AddSingleton<IContentProcessingService, ContentProcessingService>();
        services.AddSingleton<ICustomerLockService, CustomerLockService>();

        return services;
    }
}
