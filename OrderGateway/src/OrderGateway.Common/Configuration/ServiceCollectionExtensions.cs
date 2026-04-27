using CorrelationId.HttpClient;
using OrderGateway.Common.Configuration.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace OrderGateway.Common.Configuration
{
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

        public static IServiceCollection RegisterNSwagApiKeyClient<TInterface, TImplementation>(
            this IServiceCollection services,
            IConfiguration configuration
        )
            where TImplementation : TInterface
            where TInterface : class
        {
            var implementationName = typeof(TImplementation).Name;
            var options = configuration.GetSection($"Clients:{implementationName}").Get<ResourceServerApiKeyOptions>();

            if (options == null)
            {
                throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}");
            }

            if (string.IsNullOrWhiteSpace(options.ResourceBaseAddress))
            {
                throw new InvalidConfigurationException($"Missing settings for Clients:{implementationName}:ResourceBaseAddress");
            }

            if (!Uri.TryCreate(options.ResourceBaseAddress, UriKind.Absolute, out _))
            {
                throw new InvalidConfigurationException($"Invalid URI in Clients:{implementationName}:ResourceBaseAddress");
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

            services
                .AddHttpClient(
                    implementationName,
                    client =>
                    {
                        client.BaseAddress = new Uri(options.ResourceBaseAddress);
                        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                        client.DefaultRequestHeaders.Clear();
                        client.DefaultRequestHeaders.Add(options.HeaderName, options.ApiKey);
                    }
                )
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
                .AddStandardResilienceHandler();

            services.AddTransient<TInterface>(
                serviceProvider =>
                {
                    var httpClient = serviceProvider
                        .GetRequiredService<IHttpClientFactory>()
                        .CreateClient(implementationName);

                    var instance = Activator.CreateInstance(typeof(TImplementation), options.ResourceBaseAddress, httpClient);

                    if (instance == null)
                    {
                        throw new InvalidConfigurationException($"Exception creating an instance of {typeof(TImplementation)}");
                    }

                    return (TImplementation)instance;
                }
            );

            return services;
        }

        public static IServiceCollection RegisterNSwagOAuthClient<TInterface, TImplementation>(
            this IServiceCollection services,
            IConfiguration configuration
        )
            where TImplementation : TInterface
            where TInterface : class
        {
            var implementationType = typeof(TImplementation);
            var options = configuration.GetSection($"Clients:{implementationType.Name}").Get<ClientSettings>() ??
                throw new InvalidConfigurationException($"Missing settings for Clients:{implementationType.Name}");

            ValidateClientSettings(options, implementationType.Name);

            if (string.IsNullOrWhiteSpace(options.OAuthProvider))
            {
                throw new InvalidConfigurationException($"Missing settings for Clients:{implementationType.Name}:OAuthProvider");
            }

            // Read OAuth section dynamically from OAuthProvider
            var oAuthConfigSectionName = $"OAuth:{options.OAuthProvider}";
            var tokenClientName = $"{implementationType.Name}TokenClient";

            //setup the client with baseaddress and timeout.
            services
                .AddHttpClient(
                    typeof(TImplementation).Name,
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

            //setup the client oauth credentials.
            services.AddClientCredentialsTokenManagement()
            .AddClient(tokenClientName, client =>
            {
                var creds = configuration.GetRequiredSection(oAuthConfigSectionName)
                                  .Get<OAuthSettings>()
                              ?? throw new InvalidConfigurationException($"Missing settings for {oAuthConfigSectionName}");

                ValidateOAuthSettings(creds, oAuthConfigSectionName);

                client.TokenEndpoint = creds.AuthorityUrl;
                client.ClientId = creds.ClientId;
                client.ClientSecret = creds.ClientSecret;
                client.Scope = creds.Scope;
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

    }
}
