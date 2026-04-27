using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureCache(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Log.Error("Redis connection string not found. Redis is required for IDistributedCache.");
            throw new InvalidOperationException(
                "Redis is required but ConnectionStrings:Redis was not configured. " +
                "For local development, start Redis via ifx/local/start.ps1 and configure user-secrets (copy Order.Common/appsettings.localstack.json) or set ConnectionStrings__Redis."
            );
        }

        // Single Redis connection shared by both cache + locking.
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));

        services.AddStackExchangeRedisCache(options => { options.Configuration = connectionString; });

        // Ensure StackExchangeRedisCache reuses the singleton multiplexer (instead of creating its own).
        services
            .AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((options, multiplexer) =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer);
            });

        return services;
    }
}
