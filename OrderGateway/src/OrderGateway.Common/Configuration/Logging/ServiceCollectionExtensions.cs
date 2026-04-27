using Destructurama;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddSerilog(
                (serviceProvider, loggerConfiguration) =>
                    loggerConfiguration
                        .ReadFrom.Configuration(configuration)
                        .ReadFrom.Services(serviceProvider)
                        .Destructure.UsingAttributes()
            );

        return services;
    }
}
