using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("ready", () => HealthCheckResult.Healthy())
            .AddCheck<CacheHealthCheck>("cache", tags: ["dependency"]);

        return services;
    }

    private sealed class CacheHealthCheck(IDistributedCache cache) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var key = "__health_check__";
                cache.SetString(key, "ok", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
                });
                var value = cache.GetString(key);
                return Task.FromResult(value == "ok"
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("Cache round-trip failed"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Cache health check failed", ex));
            }
        }
    }
}
