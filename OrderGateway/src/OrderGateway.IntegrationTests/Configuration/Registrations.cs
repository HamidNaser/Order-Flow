using OrderGateway.Common.Configuration;
using OrderGateway.IntegrationTests.Clients.OrderApi.V0;
using OrderGateway.IntegrationTests.Clients.OrderApi.V0.Contracts;
using OrderGateway.IntegrationTests.Clients.OrderApi.V1;
using OrderGateway.IntegrationTests.Clients.OrderApi.V1.Contracts;
using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1;
using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderGateway.IntegrationTests.Configuration;

public class Registrations
{
    private readonly IServiceProvider _serviceProvider;

    public T Get<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();

    public Registrations()
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? throw new InvalidConfigurationException("Missing environment variable: DOTNET_ENVIRONMENT");
        var testSettingsJson = BrandedConfigurationFileNames.GetTestSettingsFileName(env);

        var encryptedConfiguration = new ConfigurationBuilder()
            .AddJsonFile(testSettingsJson)
            .Build();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(testSettingsJson)
            .AddDecryptedInMemoryCollection(encryptedConfiguration)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddCorrelationIdSupport(configuration)
                .RegisterNSwagApiKeyClient<IOrderGatewayApiV1Client, OrderGatewayApiV1Client>(configuration)
                .RegisterNSwagApiKeyClient<IOrderApiV0Client, OrderApiV0Client>(configuration)
                .RegisterNSwagOAuthClient<IOrderApiV1Client, OrderApiV1Client>(configuration)
                .AddDistributedMemoryCache()
            ;

        _serviceProvider = services.BuildServiceProvider();
    }
}
