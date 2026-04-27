using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services)
    {
        var healthChecks = services.AddHealthChecks();

        healthChecks
            .AddCheck("ready", () => HealthCheckResult.Healthy());

        return services;
    }
}
