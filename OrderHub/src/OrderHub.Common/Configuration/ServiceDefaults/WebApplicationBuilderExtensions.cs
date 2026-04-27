using OrderHub.Common.Configuration.Auth;
using OrderHub.Common.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static partial class Extensions
{
    public static WebApplicationBuilder AddApiDefaults(this WebApplicationBuilder builder, string configurationSectionName, string applicationName, string openApiDescription)
    {
        var bridgeAuthenticationSettings = builder.Configuration
            .GetSection(configurationSectionName)
            .GetSection(nameof(BridgeOAuthSettings))
            .Get<BridgeOAuthSettings>() ?? throw new InvalidConfigurationException();

        builder.Services
            .ConfigureApi()
            .ConfigureAuth(builder.Configuration, configurationSectionName)
            .ConfigureHealthChecks()
            .ConfigureVersioning()
#if DEBUG
            .ConfigureSwagger(
                applicationName,
                openApiDescription,
                bridgeAuthenticationSettings.Authority,
                bridgeAuthenticationSettings.Policies
            )
#endif
            .ConfigureFeatureToggle(builder.Configuration);

        return builder;
    }
}
