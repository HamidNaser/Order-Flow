using OrderGateway.Common.Configuration;
using OrderGateway.Common.FeatureToggle;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureLaunchDarkly(this IServiceCollection services, IConfiguration configuration)
    {
        var launchDarklyConfiguration = configuration
            .GetSection("LaunchDarkly")
            .Get<LaunchDarklySetting>();

        return string.IsNullOrWhiteSpace(launchDarklyConfiguration?.ApiKey)
            ? services.UseConfigFeatureFlags(configuration)
            : services.UseLaunchDarklyFeatureFlags(launchDarklyConfiguration);
    }

    private static IServiceCollection UseConfigFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Information("LaunchDarkly:ApiKey not configured, using FeatureFlags from appsettings.");

        var featureFlags = configuration
            .GetSection("FeatureFlags")
            .Get<Dictionary<string, List<FeatureUser>>>() ?? new Dictionary<string, List<FeatureUser>>();

        Log.Debug("ConfigFeatureToggle initializing with {Count} flags", featureFlags.Count);

        services.AddSingleton<IFeatureToggle>(new ConfigFeatureToggle(featureFlags));

        return services;
    }

    private static IServiceCollection UseLaunchDarklyFeatureFlags(this IServiceCollection services, LaunchDarklySetting launchDarklyConfiguration)
    {
        Log.Information("LaunchDarkly:ApiKey configured, using LaunchDarklyFeatureToggle");

        services.AddSingleton(launchDarklyConfiguration);
        services.AddSingleton<ILdClient, LdClient>(GetLaunchDarklyClient);
        services.AddSingleton<IFeatureToggleUser, MachineFeatureToggleUser>();
        services.AddSingleton<IFeatureToggle, LaunchDarklyFeatureToggle>();

        return services;
    }

    private static LdClient GetLaunchDarklyClient(IServiceProvider serviceProvider)
    {
        var launchDarklySetting = serviceProvider.GetService<LaunchDarklySetting>();

        if (string.IsNullOrWhiteSpace(launchDarklySetting?.ApiKey))
        {
            throw new InvalidConfigurationException("Missing settings service for LaunchDarkly:ApiKey");
        }

        var configuration = LaunchDarkly.Sdk.Server.Configuration
            .Builder(launchDarklySetting.ApiKey)
            .Build();

        Log.Information("LaunchDarkly client created.");

        return new LdClient(configuration);
    }
}
