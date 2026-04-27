using OrderHub.Common.Configuration.Auth;
using OrderHub.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static void ValidateBridgeOAuthSettings(BridgeOAuthSettings settings, string applicationName)
    {
        var basePath = $"{applicationName}:{nameof(BridgeOAuthSettings)}";

        if (string.IsNullOrWhiteSpace(settings.Authority))
        {
            throw new InvalidConfigurationException($"Missing settings for {basePath}:Authority");
        }

        if (!Uri.TryCreate(settings.Authority, UriKind.Absolute, out _))
        {
            throw new InvalidConfigurationException($"Invalid URI in {basePath}:Authority");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidConfigurationException($"Missing settings for {basePath}:Audience");
        }

        if (settings.Policies == null || settings.Policies.Length == 0)
        {
            throw new InvalidConfigurationException($"Missing settings for {basePath}:Policies");
        }

        foreach (var policy in settings.Policies)
        {
            if (string.IsNullOrWhiteSpace(policy.Name))
            {
                throw new InvalidConfigurationException($"Missing policy Name in {basePath}:Policies");
            }

            if (policy.Scopes == null || policy.Scopes.Length == 0)
            {
                throw new InvalidConfigurationException($"Missing policy Scopes in {basePath}:Policies:{policy.Name}");
            }

            if (policy.Scopes.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidConfigurationException($"Invalid policy Scopes in {basePath}:Policies:{policy.Name}");
            }
        }
    }

    public static IServiceCollection ConfigureAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName
    )
    {
        var apiKeyAuthenticationOptions = configuration
            .GetSection(applicationName)
            .GetSection(nameof(ApiKeyAuthenticationOptions))
            .Get<ApiKeyAuthenticationOptions>();

        var bridgeAuthenticationSettings = configuration
            .GetSection(applicationName)
            .GetSection(nameof(BridgeOAuthSettings))
            .Get<BridgeOAuthSettings>() ?? throw new InvalidConfigurationException();

        ValidateBridgeOAuthSettings(bridgeAuthenticationSettings, applicationName);

        var authenticationBuilder = services
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
            .AddJwtBearer(
                BridgeOAuthSettings.AuthenticationScheme,
                options =>
                {
                    options.Authority = bridgeAuthenticationSettings.Authority;
                    options.Audience = bridgeAuthenticationSettings.Audience;

                    if (Uri.TryCreate(bridgeAuthenticationSettings.Authority, UriKind.Absolute, out var authorityUri))
                    {
                        options.RequireHttpsMetadata = authorityUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
                    }
                }
            );

        var authorizationBuilder = services.AddAuthorizationBuilder();

        if (apiKeyAuthenticationOptions != null)
        {
            if (string.IsNullOrWhiteSpace(apiKeyAuthenticationOptions.ApiKey))
                throw new InvalidConfigurationException();

            authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                options => { options.ApiKey = apiKeyAuthenticationOptions.ApiKey; }
            );

            authorizationBuilder.AddPolicy(
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
        }

        // Register all policies from BridgeOAuthSettings
        foreach (var policy in bridgeAuthenticationSettings.Policies)
        {
            authorizationBuilder.AddPolicy(
                policy.Name,
                policyBuilder =>
                {
                    policyBuilder.AddAuthenticationSchemes(BridgeOAuthSettings.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireScope(bridgeAuthenticationSettings, policy.Scopes);
                }
            );
        }

        return services;
    }

    public static AuthorizationPolicyBuilder RequireScope(
        this AuthorizationPolicyBuilder builder,
        BridgeOAuthSettings bridgeAuthSettings,
        string[] requiredScopes
    )
    {
        return builder.RequireAssertion(context =>
            {
                var claims = context.User?.Claims.ToList() ?? [];
                var issuer = claims.FirstOrDefault(x => x.Type == "iss")?.Value;
                string[] scopesToValidate;
                string[] tokenScopes;

                if (
                    !string.IsNullOrWhiteSpace(issuer)
                    && string.Equals(issuer, bridgeAuthSettings.Authority, StringComparison.OrdinalIgnoreCase)
                )
                {
                    scopesToValidate = requiredScopes;
                    tokenScopes = claims
                        .Where(c =>
                            c.Type == BridgeOAuthSettings.BridgeScopeClaim
                            || c.Type.Equals("scope", StringComparison.OrdinalIgnoreCase)
                            || c.Type.Equals("scp", StringComparison.OrdinalIgnoreCase)
                        )
                        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
                else
                {
                    return false;
                }

                if (tokenScopes.Length == 0)
                {
                    return false;
                }

                var tokenScopeSet = tokenScopes.ToHashSet();

                return scopesToValidate.All(tokenScopeSet.Contains);
            }
        );
    }
}
