using Microsoft.Extensions.Configuration;
using Serilog;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureCache(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Log.Warning("Redis connection string not found. Binding IDistributedCache to memory cache");
            services.AddDistributedMemoryCache();
        }
        else
        {
            Log.Warning("Redis connection string found. Binding IDistributedCache to redis cache");

            services.AddStackExchangeRedisCache(options => { options.Configuration = connectionString; });
        }

        return services;
    }
}
