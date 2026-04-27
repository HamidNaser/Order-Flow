using System.Collections.Concurrent;
using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Services;
using OrderHub.Common.Configuration.Channels;
using OrderHub.IntegrationTests.Clients.OrderApi.V0;
using OrderHub.IntegrationTests.Clients.OrderApi.V0.Contracts;
using OrderHub.IntegrationTests.Clients.OrderApi.V1;
using OrderHub.IntegrationTests.Clients.OrderApi.V1.Contracts;
using OrderHub.IntegrationTests.Clients.IngestStandardApi.V1;
using OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts;
using OrderHub.IntegrationTests.Clients.IngestExpressApi.V1;
using OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts;
using OrderHub.IntegrationTests.Configuration;
using OrderHub.IntegrationTests.Helpers;
using OrderHub.IntegrationTests.IngestExpressApi.Helpers;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Xunit;

namespace OrderHub.IntegrationTests;

public class ApiTestsFixture : IAsyncLifetime
{
    public IOrderApiV0Client OrderApiV0Client { get; set; }
    public IOrderApiV1Client OrderApiV1Client { get; set; }
    public IIngestExpressApiV1Client IngestExpressApiV1Client { get; set; }
    public IIngestStandardApiV1Client IngestStandardApiV1Client { get; set; }

    public IConfiguration Configuration { get; }

    public OrderSummaryConfig OrderSummaryConfig { get; set; }
    public CustomerTestHelper CustomerTestHelper { get; set; }

    private ConcurrentBag<(string storeId, string orderId)> CreatedOrderIds { get; set; } = new();

    private readonly AsyncRetryPolicy _notFoundRetryPolicy = Policy
        .Handle<Exception>(ex =>
        {
            return ex switch
            {
                OrderApiV0ClientException clientException => clientException.StatusCode == 404,
                OrderApiV1ClientException clientException => clientException.StatusCode == 404,
                IngestExpressApiV1ClientException clientException => clientException.StatusCode == 404,
                IngestStandardApiV1ClientException clientException => clientException.StatusCode == 404,
                _ => false
            };
        })
        .WaitAndRetryAsync(Enumerable.Repeat(TimeSpan.FromSeconds(3), 60));

    public ApiTestsFixture()
    {
        var registrations = new Registrations();

        OrderApiV0Client = registrations.Get<IOrderApiV0Client>();
        OrderApiV1Client = registrations.Get<IOrderApiV1Client>();

        // Wrap the Ingest OAuth API clients with auto-register wrappers
        var baseIngestExpressOAuthClient = (IngestExpressApiV1Client)registrations.Get<IIngestExpressApiV1Client>();
        var baseIngestStandardOAuthClient = (IngestStandardApiV1Client)registrations.Get<IIngestStandardApiV1Client>();

        IngestExpressApiV1Client = registrations.WrapIngestExpressClient(baseIngestExpressOAuthClient, this);
        IngestStandardApiV1Client = registrations.WrapIngestStandardClient(baseIngestStandardOAuthClient, this);

        OrderSummaryConfig = registrations.Get<OrderSummaryConfig>();

        // Initialize CustomerTestHelper for creating real customers in tests
        CustomerTestHelper = new CustomerTestHelper();

        // Initialize test data generators with CustomerTestHelper
        IngestExpressTestDataGenerator.Initialize(CustomerTestHelper);

        Configuration = registrations.Get<IConfiguration>();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

   public void RegisterOrder(S3OrderKey orderKey, string storeId)
   {
       CreatedOrderIds.Add((storeId, orderKey.OrderId));
   }

       public Task DisposeAsync()
       {
           return CleanupTestDataAsync();
       }

       private async Task CleanupTestDataAsync()
       {
           var groupedByStoreId = CreatedOrderIds
               .Distinct()
               .GroupBy(x => x.storeId)
               .ToList();

           foreach (var group in groupedByStoreId)
           {
               var orderIds = group.Select(x => x.orderId).ToList();
               if (orderIds.Count != 0)
               {
                   try
                   {
                       await OrderApiV0Client.BulkDeleteOrdersAsync(group.Key, orderIds);
                   }
                   catch
                   {
                       // Cleanup failures shouldn't break tests
                   }
               }
           }
       }

    public async Task<T0> RetryUntilExistsAsync<T0>(Func<Task<T0>> checkExistsFunc)
    {
        var result = await _notFoundRetryPolicy.ExecuteAsync(async () => await checkExistsFunc());

        return result;
    }
}

[CollectionDefinition("ApiTests")]
public class ApiTestsCollection : ICollectionFixture<ApiTestsFixture>;

