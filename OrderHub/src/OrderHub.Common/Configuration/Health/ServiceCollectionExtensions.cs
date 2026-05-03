using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("ready", () => HealthCheckResult.Healthy())
            .AddCheck<MongoDbHealthCheck>("mongodb", tags: ["dependency"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["dependency"]);

        return services;
    }

    private sealed class MongoDbHealthCheck(IMongoClient client) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = client.GetDatabase("admin");
                db.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                return Task.FromResult(HealthCheckResult.Healthy());
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("MongoDB ping failed", ex));
            }
        }
    }

    private sealed class RedisHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = multiplexer.GetDatabase();
                db.Ping();
                return Task.FromResult(HealthCheckResult.Healthy());
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Redis ping failed", ex));
            }
        }
    }
}
