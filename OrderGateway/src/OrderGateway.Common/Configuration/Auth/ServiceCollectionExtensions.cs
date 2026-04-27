using OrderGateway.Common.Configuration;
using OrderGateway.Common.Configuration.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        ApplicationName applicationName
    )
    {
        var apiKeyAuthenticationOptions = configuration
            .GetSection(applicationName.ToString())
            .GetSection(nameof(ApiKeyAuthenticationOptions))
            .Get<ApiKeyAuthenticationOptions>();

        if (string.IsNullOrWhiteSpace(apiKeyAuthenticationOptions?.ApiKey))
        {
            throw new InvalidConfigurationException();
        }

        services
            .AddAuthentication(
                options =>
                {
                    options.DefaultAuthenticateScheme = nameof(NoResultAuthenticationHandler);
                    options.DefaultForbidScheme = nameof(NoResultAuthenticationHandler);
                    options.AddScheme<NoResultAuthenticationHandler>(
                        nameof(NoResultAuthenticationHandler),
                        nameof(NoResultAuthenticationHandler)
                    );
                }
            )
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                options => { options.ApiKey = apiKeyAuthenticationOptions.ApiKey; }
            );

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                ApiKeyAuthenticationDefaults.AuthorizationPolicy,
                apiKeyAuthenticationOptions.AllowAnonymous
                    ? new AuthorizationPolicyBuilder()
                        .RequireAssertion(_ => true)
                        .Build()
                    : new AuthorizationPolicyBuilder()
                        .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .Build()
            );

        return services;
    }
}
