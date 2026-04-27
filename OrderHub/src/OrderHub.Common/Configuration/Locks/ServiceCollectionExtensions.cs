using OrderHub.Common.Configuration.Locks;
using Order.MessagePump.Locks;
using Order.MessagePump.Redis.Locks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection ConfigureLocking(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LockingOptions>(configuration.GetSection("Locking"));

        var connectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Log.Error("Redis connection string not found. Redis is required for ILockManager.");
            throw new InvalidOperationException(
                "Redis is required for locking but ConnectionStrings:Redis was not configured. " +
                "For local development, start Redis via ifx/local/start.ps1 and configure user-secrets (copy Order.Common/appsettings.localstack.json) or set ConnectionStrings__Redis."
            );
        }

        services.TryAddSingleton<ILockManager>(sp => new SimpleRedisLockManager(sp.GetRequiredService<IConnectionMultiplexer>()));

        return services;
    }
}