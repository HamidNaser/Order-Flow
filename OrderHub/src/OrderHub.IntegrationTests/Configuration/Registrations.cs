using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Configuration;
using OrderHub.IntegrationTests.Clients.OrderApi.V0;
using OrderHub.IntegrationTests.Clients.OrderApi.V0.Contracts;
using OrderHub.IntegrationTests.Clients.OrderApi.V1;
using OrderHub.IntegrationTests.Clients.OrderApi.V1.Contracts;
using OrderHub.IntegrationTests.Clients.IngestExpressApi.V1;
using OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts;
using OrderHub.IntegrationTests.Clients.IngestStandardApi.V1;
using OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts;
using CorrelationId.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderHub.IntegrationTests.Configuration;

public class Registrations
{
    private readonly IServiceProvider _serviceProvider;

    public T0 Get<T0>() where T0 : notnull => _serviceProvider.GetRequiredService<T0>();

    public Registrations()
    {
        var testSettingsJson = BrandedConfigurationFileNames.GetTestSettingsFileName(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"));

        var encryptedConfiguration = new ConfigurationBuilder()
            .AddJsonFile(testSettingsJson)
            .Build();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(testSettingsJson)
            .AddDecryptedInMemoryCollection(encryptedConfiguration)
            .AddEnvironmentVariables()
            .Build();


        var s3Config = configuration.GetRequiredSection("S3Config").Get<S3Config>()
            ?? throw new NullReferenceException("S3Config is null");

        var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddDefaultCorrelationId(config =>
                {
                    config.AddToLoggingScope = true;
                    config.LoggingScopeKey = "XOrderCorrelationId";
                    config.RequestHeader = "X-Order-Correlation-Id";
                })
                .RegisterNSwagApiKeyClient<IOrderApiV0Client, OrderApiV0Client>(configuration)
                .RegisterNSwagOAuthClient<IOrderApiV1Client, OrderApiV1Client>(configuration)
                .RegisterNSwagOAuthClient<IIngestExpressApiV1Client, IngestExpressApiV1Client>(configuration)
                .RegisterNSwagOAuthClient<IIngestStandardApiV1Client, IngestStandardApiV1Client>(configuration)
                .RegisterPreviewConfig(configuration)
                .AddDistributedMemoryCache()
                .AddSingleton(s3Config)
            ;

        _serviceProvider = services.BuildServiceProvider();
    }

    public IIngestExpressApiV1Client WrapIngestExpressClient(IngestExpressApiV1Client baseClient, ApiTestsFixture fixture)
    {
        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("IngestExpressApiV1Client");
        return new IngestExpressApiV1ClientWithAutoRegister(baseClient.BaseUrl, httpClient, fixture);
    }

    public IIngestStandardApiV1Client WrapIngestStandardClient(IngestStandardApiV1Client baseClient, ApiTestsFixture fixture)
    {
        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("IngestStandardApiV1Client");
        return new IngestStandardApiV1ClientWithAutoRegister(baseClient.BaseUrl, httpClient, fixture);
    }
}

