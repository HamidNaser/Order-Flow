using NSubstitute;
using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Managers;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;
using OrderHub.Contracts.Utility;
using Xunit;
using Priority = OrderHub.Common.Models.Components.Priority;
using OrderFlowType = OrderHub.Contracts.Common.Enums.OrderFlowType;
using FulfillmentStatus = OrderHub.Contracts.Common.Enums.FulfillmentStatus;
using MerchantName = OrderHub.Contracts.Common.Enums.MerchantName;

namespace OrderHub.UnitTests.Managers;

public class OrderIngestManagerTests
{
    private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
    private readonly S3Config _s3Config = new() { OrderBucketName = "test-orders-bucket" };
    private readonly OrderIngestManager _sut;

    public OrderIngestManagerTests()
    {
        _sut = new OrderIngestManager(_s3Service, _s3Config);
    }

    // ──────────────────────────────────────────────
    // AddOrderAsync — new order
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddOrderAsync_NewOrder_ReturnsNewOrderStatus()
    {
        // Arrange
        var request = CreateOrderRequest();
        _s3Service.GetObjectKeysByPrefix(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<string>());

        // Act
        var result = await _sut.AddOrderAsync(request, Priority.STANDARD);

        // Assert
        Assert.Equal(AddOrderResultStatus.NEW_ORDER, result.Status);
        Assert.NotNull(result.OrderId);
        Assert.NotEmpty(result.OrderId);
    }

    [Fact]
    public async Task AddOrderAsync_NewOrder_PersistsToS3()
    {
        // Arrange
        var request = CreateOrderRequest();
        _s3Service.GetObjectKeysByPrefix(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<string>());

        // Act
        await _sut.AddOrderAsync(request, Priority.EXPRESS);

        // Assert
        await _s3Service.Received(1).PutObjectAsync(
            Arg.Is<S3PutObjectRequest<OrderRequest>>(r =>
                r.BucketName == "test-orders-bucket" &&
                r.Payload == request));
    }

    // ──────────────────────────────────────────────
    // AddOrderAsync — duplicate detection
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddOrderAsync_DuplicateExists_ReturnsDuplicateStatus()
    {
        // Arrange
        var request = CreateOrderRequest();
        var existingKey = "STANDARD/PRIME/STANDARD/source123/64f1234567890abcdef12345";
        _s3Service.GetObjectKeysByPrefix(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<string> { existingKey });

        // Act
        var result = await _sut.AddOrderAsync(request, Priority.STANDARD);

        // Assert
        Assert.Equal(AddOrderResultStatus.DUPLICATE_REQUEST, result.Status);
        Assert.Equal("64f1234567890abcdef12345", result.OrderId);
    }

    [Fact]
    public async Task AddOrderAsync_DuplicateExists_DoesNotPersistToS3()
    {
        // Arrange
        var request = CreateOrderRequest();
        var existingKey = "STANDARD/PRIME/STANDARD/source123/64f1234567890abcdef12345";
        _s3Service.GetObjectKeysByPrefix(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<string> { existingKey });

        // Act
        await _sut.AddOrderAsync(request, Priority.STANDARD);

        // Assert
        await _s3Service.DidNotReceiveWithAnyArgs().PutObjectAsync(Arg.Any<S3PutObjectRequest<OrderRequest>>());
    }

    [Fact]
    public async Task AddOrderAsync_MultipleS3KeysExist_UsesFirstMatch()
    {
        // Arrange
        var request = CreateOrderRequest();
        var keys = new List<string>
        {
            "STANDARD/PRIME/STANDARD/source123/64f1234567890abcdef12345",
            "STANDARD/PRIME/STANDARD/source123/64f1234567890abcdef12346"
        };
        _s3Service.GetObjectKeysByPrefix(Arg.Any<string>(), Arg.Any<string>())
            .Returns(keys);

        // Act
        var result = await _sut.AddOrderAsync(request, Priority.STANDARD);

        // Assert
        Assert.Equal(AddOrderResultStatus.DUPLICATE_REQUEST, result.Status);
        Assert.Equal("64f1234567890abcdef12345", result.OrderId);
    }

    [Fact]
    public async Task AddOrderAsync_ExistingKeyUnparseable_TreatsAsNewOrder()
    {
        // Arrange
        var request = CreateOrderRequest();
        _s3Service.GetObjectKeysByPrefix(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<string> { "unparseable/key" });

        // Act
        var result = await _sut.AddOrderAsync(request, Priority.STANDARD);

        // Assert
        Assert.Equal(AddOrderResultStatus.NEW_ORDER, result.Status);
    }

    // ──────────────────────────────────────────────
    // AddOrderAsync — duplicate protection prefix
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddOrderAsync_ChecksPrefixWithCorrectComponents()
    {
        // Arrange
        var request = CreateOrderRequest();
        _s3Service.GetObjectKeysByPrefix(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<string>());

        // Act
        await _sut.AddOrderAsync(request, Priority.EXPRESS);

        // Assert
        await _s3Service.Received(1).GetObjectKeysByPrefix(
            "test-orders-bucket",
            "EXPRESS/PRIME/STANDARD/source123");
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static AddShipmentOrderRequest CreateOrderRequest()
    {
        return new AddShipmentOrderRequest
        {
            CustomerId = "customer-1",
            StoreId = "store-1",
            OrderFlow = OrderFlowType.INCOMING,
            Content = "Test content",
            OrderPlacedDate = DateTimeOffset.UtcNow,
            Merchant = new OrderHub.Contracts.Common.Merchant { Name = MerchantName.PRIME, OrderId = "source123" },
            FulfillmentStatus = FulfillmentStatus.SUCCESS,
            OrderFulfilledDate = DateTimeOffset.UtcNow,
            To = [new OrderHub.Contracts.Common.AddressInfo { Address = "addr1" }],
            From = new OrderHub.Contracts.Common.AddressInfo { Address = "addr2" }
        };
    }
}
