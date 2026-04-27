using OrderHub.Common.Exceptions;
using OrderHub.Common.Repositories;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureResourceAccess(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDb(configuration)
            .AddRepositories();

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddTransient<IOrderRepository, OrderRepository>();

        return services;
    }

    private static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddMongoClient(configuration)
            .AddMongoDbSerializer();

        return services;
    }

    private static IServiceCollection AddMongoDbSerializer(this IServiceCollection services)
    {
        var conventionPack = new ConventionPack()
        {
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true),
            new IgnoreIfNullConvention(true)
        };

        ConventionRegistry.Register("SerializationConventions", conventionPack, (_) => true);

        return services;
    }

    private static IServiceCollection AddMongoClient(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDB") ?? throw new InvalidConfigurationException();

        services.AddSingleton<IMongoClient>(sp =>
        {
            MongoClientSettings settings = MongoClientSettings.FromConnectionString(connectionString);

            if (configuration.GetValue<bool>("DatabaseLoggingEnabled"))
            {
                settings.ClusterConfigurator = cb =>
                {
                    cb.Subscribe<CommandStartedEvent>(cse =>
                    {
                        Console.WriteLine(cse.Command);
                    });
                };
            }

            return new MongoClient(settings);
        });

        return services;
    }
}
