using OrderGateway.IntegrationTests.Clients.OrderApi.V0.Contracts;
using OrderGateway.IntegrationTests.Clients.OrderApi.V1.Contracts;
using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts;
using OrderGateway.IntegrationTests.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace OrderGateway.IntegrationTests;

public class ApiTestsFixture : IAsyncLifetime
{
    public IConfiguration Configuration { get; }

    public IOrderGatewayApiV1Client OrderGatewayApiV1Client { get; set; }

    public IOrderApiV1Client OrderApiV1Client { get; set; }

    public IOrderApiV0Client OrderApiV0Client { get; set; }

    public string Environment { get; }

    public int StoreId { get; }

    public ApiTestsFixture()
    {
        var registrations = new Registrations();

        // Capture environment values once for all tests
        var dotNetEnvironment = System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var aspNetCoreEnvironment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment = !string.IsNullOrWhiteSpace(dotNetEnvironment)
            ? dotNetEnvironment!
            : (!string.IsNullOrWhiteSpace(aspNetCoreEnvironment) ? aspNetCoreEnvironment! : "Development");

        OrderGatewayApiV1Client = registrations.Get<IOrderGatewayApiV1Client>();
        OrderApiV1Client = registrations.Get<IOrderApiV1Client>();
        OrderApiV0Client = registrations.Get<IOrderApiV0Client>();

        // Load StoreId from DI-managed configuration
        Configuration = registrations.Get<IConfiguration>();
        var config = Configuration;
        StoreId = config.GetValue<int>("StoreId");

        TestData.TestConfig.StoreId = StoreId;
        TestData.TestConfig.CloudContentKey = config.GetValue<string>("CloudContentKey") ?? string.Empty;
        TestData.TestConfig.CloudContentValue = config.GetValue<string>("CloudContentValue") ?? string.Empty;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}

[CollectionDefinition("ApiTests")]
public class ApiTestsCollection : ICollectionFixture<ApiTestsFixture>;
