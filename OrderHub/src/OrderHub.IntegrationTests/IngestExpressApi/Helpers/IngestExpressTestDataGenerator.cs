using Bogus;
using OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts;
using OrderHub.IntegrationTests.Helpers;
using FulfillmentStatus = OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.FulfillmentStatus;
using OrderFlowType = OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.OrderFlowType;
using MerchantName = OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.MerchantName;

namespace OrderHub.IntegrationTests.IngestExpressApi.Helpers;

public static class IngestExpressTestDataGenerator
{
    private static readonly Faker Faker = new();

    private static CustomerTestHelper? _customerTestHelper;

    public static void Initialize(CustomerTestHelper customerTestHelper)
    {
        _customerTestHelper = customerTestHelper;
    }

    private static Platform GenerateExternalPlatform() => new()
    {
        Id = Faker.PickRandom<PlatformId>(),
        OperationId = Faker.Random.Guid().ToString(),
        CustomerId = Faker.Random.Guid().ToString(),
        CustomerName = Faker.Name.FullName(),
        AgentId = Faker.Random.Guid().ToString(),
        AgentName = Faker.Name.FullName(),
        TrackingId = Faker.Random.Guid().ToString(),
    };

    private static Merchant GenerateExternalMerchant() => new()
    {
        Name = MerchantName.INTEGRATION_TEST,
        OrderId = Faker.Random.Guid().ToString(),
        SourceApplication = Faker.Random.Word()
    };

    public static async Task<AddDigitalOrderRequest> GenerateAddDigitalOrderRequestAsync()
    {
        if (_customerTestHelper == null)
            throw new InvalidOperationException("CustomerTestHelper must be initialized before generating requests. Call Initialize() first.");

        var platform = GenerateExternalPlatform();
        var storeId = Faker.PickRandom(TestConstants.TestStoreIds);
        var direction = Faker.PickRandom<OrderFlowType>();
        var toPhoneNumber = PhoneNumberGenerator.GetRandomPhoneNumber(Region.US);
        var fromPhoneNumber = PhoneNumberGenerator.GetRandomPhoneNumber(Region.US);

        // Create customer with phone number matching the direction (customer is To for OUTGOING, From for INCOMING)
        var customerPhoneNumber = direction == OrderFlowType.OUTGOING ? toPhoneNumber : fromPhoneNumber;
        var (customerId, customerName) = await _customerTestHelper.CreateCustomerAsync(storeId, phoneNumber: PhoneNumberGenerator.GetLast10Numbers(customerPhoneNumber));

        return new AddDigitalOrderRequest
        {
            StoreId = storeId,
            CustomerId = customerId,
            CustomerName = customerName,
            AgentId = Faker.Random.Guid().ToString(),
            AgentName = platform.AgentName,
            OrderFlow = direction,
            Content = Faker.Lorem.Paragraphs(4),
            OrderPlacedDate = Faker.Date.RecentOffset(3),
            OrderFulfilledDate = Faker.Date.RecentOffset(3),
            Merchant = GenerateExternalMerchant(),
            TenantId = Faker.Random.Guid().ToString(),
            FulfillmentStatus = FulfillmentStatus.SUCCESS,
            Platform = GenerateExternalPlatform(),
            ToPhoneNumber = toPhoneNumber,
            FromPhoneNumber = fromPhoneNumber,
        };
    }

    public static async Task<AddShipmentOrderRequest> GenerateAddShipmentOrderRequestAsync()
    {
        if (_customerTestHelper == null)
            throw new InvalidOperationException("CustomerTestHelper must be initialized before generating requests. Call Initialize() first.");

        var platform = GenerateExternalPlatform();
        var storeId = Faker.PickRandom(TestConstants.TestStoreIds);
        var direction = Faker.PickRandom<OrderFlowType>();
        var toAddress = $"ORD-TO-{Faker.Random.AlphaNumeric(8).ToUpper()}";
        var fromAddress = $"ORD-FROM-{Faker.Random.AlphaNumeric(8).ToUpper()}";

        // Create customer with address matching the direction (customer is To for OUTGOING, From for INCOMING)
        var customerAddress = direction == OrderFlowType.OUTGOING ? toAddress : fromAddress;
        var (customerId, customerName) = await _customerTestHelper.CreateCustomerAsync(storeId, orderAddress: customerAddress);

        return new AddShipmentOrderRequest
        {
            To = [new AddressInfo { Address = toAddress, Name = Faker.Name.FullName() }],
            From = new AddressInfo { Address = fromAddress, Name = Faker.Name.FullName() },
            OrderTitle = Faker.Lorem.Sentence(),
            StoreId = storeId,
            CustomerId = customerId,
            CustomerName = customerName,
            AgentId = Faker.Random.Guid().ToString(),
            AgentName = platform.AgentName,
            OrderFlow = direction,
            Content = Faker.Lorem.Paragraphs(4),
            OrderPlacedDate = Faker.Date.RecentOffset(3),
            OrderFulfilledDate = Faker.Date.RecentOffset(3),
            Merchant = GenerateExternalMerchant(),
            TenantId = Faker.Random.Guid().ToString(),
            FulfillmentStatus = FulfillmentStatus.SUCCESS,
            Platform = GenerateExternalPlatform(),
        };
    }
}
