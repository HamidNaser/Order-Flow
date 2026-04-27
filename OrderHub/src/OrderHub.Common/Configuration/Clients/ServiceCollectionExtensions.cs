using CorrelationId.HttpClient;
using OrderHub.Common.Configuration.Clients;
using OrderHub.Common.Exceptions;
using Duende.AccessTokenManagement;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static void ValidateClientSettings(ClientSettings options, string implementationName)
    {
        if (string.IsNullOrWhiteSpace(options.BaseAddress))
        {
            throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}:BaseAddress");
        }

        if (!Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out _))
        {
            throw new InvalidConfigurationException($"Invalid URI in Clients:{implementationName}:BaseAddress");
        }

        if (options.TimeoutSeconds <= 0)
        {
            throw new InvalidConfigurationException($"Invalid settings for Clients:{implementationName}:TimeoutSeconds. Value must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(options.OAuthProvider))
        {
            throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}:OAuthProvider");
        }
    }

    private static void ValidateOAuthSettings(OAuthSettings creds, string oAuthConfigSectionName)
    {
        if (string.IsNullOrWhiteSpace(creds.AuthorityUrl))
        {
            throw new InvalidConfigurationException($"Missing settings for {oAuthConfigSectionName}:AuthorityUrl");
        }

        if (!Uri.TryCreate(creds.AuthorityUrl, UriKind.Absolute, out _))
        {
            throw new InvalidConfigurationException($"Invalid URI in {oAuthConfigSectionName}:AuthorityUrl");
        }

        if (string.IsNullOrWhiteSpace(creds.ClientId))
        {
            throw new InvalidConfigurationException($"Missing settings for {oAuthConfigSectionName}:ClientId");
        }

        if (string.IsNullOrWhiteSpace(creds.ClientSecret))
        {
            throw new InvalidConfigurationException($"Missing settings for {oAuthConfigSectionName}:ClientSecret");
        }

        if (string.IsNullOrWhiteSpace(creds.Scope))
        {
            throw new InvalidConfigurationException($"Missing settings for {oAuthConfigSectionName}:Scope");
        }
    }

    public static IServiceCollection RegisterNSwagOAuthClient<TInterface, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TImplementation : TInterface
        where TInterface : class
    {
        var implementationType = typeof(TImplementation);
        var implementationName = implementationType.Name;
        var tokenClientName = ClientCredentialsClientName.Parse($"{implementationName}TokenClient");

        var options = configuration.GetSection($"Clients:{implementationName}").Get<ClientSettings>() ??
            throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}");

        ValidateClientSettings(options, implementationName);

        // Read OAuth section dynamically from OAuthProvider
        var oAuthConfigSectionName = $"OAuth:{options.OAuthProvider}";

        // Setup the client with base address and timeout
        services
            .AddHttpClient(
                implementationName,
                client =>
                {
                    client.BaseAddress = new Uri(options.BaseAddress);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddClientCredentialsTokenHandler(tokenClientName)
            .AddCorrelationIdForwarding()
            .AddStandardResilienceHandler();

        // Setup the client OAuth credentials
        services.AddClientCredentialsTokenManagement()
        .AddClient(tokenClientName, client =>
        {
            var creds = configuration.GetRequiredSection(oAuthConfigSectionName)
                              .Get<OAuthSettings>()
                          ?? throw new InvalidConfigurationException($"Missing settings for {oAuthConfigSectionName}");

            ValidateOAuthSettings(creds, oAuthConfigSectionName);

            client.TokenEndpoint = new Uri(creds.AuthorityUrl);
            client.ClientId = ClientId.Parse(creds.ClientId);
            client.ClientSecret = ClientSecret.Parse(creds.ClientSecret);
            client.Scope = Scope.Parse(creds.Scope);
        });

        services.AddTransient<TInterface>(
            serviceProvider =>
            {
                var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(TImplementation).Name);

                // Attempt to construct NSwag client using either (HttpClient) or (string baseUrl, HttpClient)
                object? instance = null;

                // First try constructor with HttpClient only
                var ctorWithHttpClient = implementationType.GetConstructor([typeof(HttpClient)]);
                if (ctorWithHttpClient != null)
                {
                    instance = Activator.CreateInstance(implementationType, httpClient);
                }
                else
                {
                    // Fallback: try (string baseUrl, HttpClient)
                    var ctorWithBaseAndHttp = implementationType.GetConstructor([typeof(string), typeof(HttpClient)]);
                    if (ctorWithBaseAndHttp != null)
                    {
                        instance = Activator.CreateInstance(implementationType, options.BaseAddress, httpClient);
                    }
                }

                if (instance == null)
                {
                    throw new InvalidConfigurationException($"Exception creating an instance of {implementationType}. Expected constructor (HttpClient) or (string baseUrl, HttpClient)");
                }
                return (TImplementation)instance;
            }
        );
        return services;
    }

    public static IServiceCollection RegisterNSwagApiKeyClient<TInterface, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TImplementation : TInterface
        where TInterface : class
    {
        var implementationType = typeof(TImplementation);
        var implementationName = implementationType.Name;

        var options = configuration.GetSection($"Clients:{implementationName}").Get<ApiKeyClientSettings>() ??
            throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}");

        if (string.IsNullOrWhiteSpace(options.BaseAddress))
        {
            throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}:BaseAddress");
        }

        if (!Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out _))
        {
            throw new InvalidConfigurationException($"Invalid URI in Clients:{implementationName}:BaseAddress");
        }

        if (options.TimeoutSeconds <= 0)
        {
            throw new InvalidConfigurationException($"Invalid settings for Clients:{implementationName}:TimeoutSeconds. Value must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}:HeaderName");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}:ApiKey");
        }

        // Setup the client with base address, timeout, and API key header
        services
            .AddHttpClient(
                implementationName,
                client =>
                {
                    client.BaseAddress = new Uri(options.BaseAddress);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add(options.HeaderName, options.ApiKey);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddCorrelationIdForwarding()
            .AddStandardResilienceHandler();

        services.AddTransient<TInterface>(
            serviceProvider =>
            {
                var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(implementationName);

                // Attempt to construct NSwag client using either (HttpClient) or (string baseUrl, HttpClient)
                object? instance = null;

                // First try constructor with HttpClient only
                var ctorWithHttpClient = implementationType.GetConstructor([typeof(HttpClient)]);
                if (ctorWithHttpClient != null)
                {
                    instance = Activator.CreateInstance(implementationType, httpClient);
                }
                else
                {
                    // Fallback: try (string baseUrl, HttpClient)
                    var ctorWithBaseAndHttp = implementationType.GetConstructor([typeof(string), typeof(HttpClient)]);
                    if (ctorWithBaseAndHttp != null)
                    {
                        instance = Activator.CreateInstance(implementationType, options.BaseAddress, httpClient);
                    }
                }

                if (instance == null)
                {
                    throw new InvalidConfigurationException($"Exception creating an instance of {implementationType}. Expected constructor (HttpClient) or (string baseUrl, HttpClient)");
                }
                return (TImplementation)instance;
            }
        );
        return services;
    }
}
