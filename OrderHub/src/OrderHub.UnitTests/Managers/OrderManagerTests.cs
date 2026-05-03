using NSubstitute;
using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Managers;
using OrderHub.Common.Models;
using OrderHub.Common.Repositories;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;
using OrderHub.Contracts.Utility;
using Xunit;
using Priority = OrderHub.Common.Models.Components.Priority;
using OrderFlowType = OrderHub.Contracts.Common.Enums.OrderFlowType;
using MerchantName = OrderHub.Common.Models.Components.MerchantName;
using FulfillmentStatus = OrderHub.Common.Models.Components.FulfillmentStatus;
using Merchant = OrderHub.Common.Models.Components.Merchant;
using AddressInfo = OrderHub.Common.Models.Components.AddressInfo;

namespace OrderHub.UnitTests.Managers;

public class OrderManagerTests
{
    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();
    private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
    private readonly S3Config _s3Config = new() { OrderBucketName = "test-bucket" };
    private readonly OrderManager _sut;

    public OrderManagerTests()
    {
        _sut = new OrderManager(_repository, _s3Service, _s3Config);
    }

    // ──────────────────────────────────────────────
    // ReadCustomerOrdersAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadCustomerOrdersAsync_ReturnsCountAndOrders()
    {
        // Arrange
        var orders = new List<ChannelOrder> { CreateShipmentOrder("order-1") };
        _repository.ReadCustomerOrdersCountAsync("store-1", "customer-1").Returns(1L);
        _repository.ReadCustomerOrdersAsync("store-1", "customer-1", 25, 0).Returns(orders);

        // Act
        var (count, results) = await _sut.ReadCustomerOrdersAsync("store-1", "customer-1");

        // Assert
        Assert.Equal(1, count);
        Assert.Single(results);
    }

    [Fact]
    public async Task ReadCustomerOrdersAsync_CalculatesOffsetFromPage()
    {
        // Arrange
        _repository.ReadCustomerOrdersCountAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(0L);
        _repository.ReadCustomerOrdersAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(new List<ChannelOrder>());

        // Act
        await _sut.ReadCustomerOrdersAsync("store-1", "customer-1", page: 3, pageSize: 10);

        // Assert — offset = pageSize * (page - 1) = 10 * 2 = 20
        await _repository.Received(1).ReadCustomerOrdersAsync("store-1", "customer-1", 10, 20);
    }

    // ──────────────────────────────────────────────
    // BulkDeleteOrdersAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task BulkDeleteOrdersAsync_DelegatesToRepository()
    {
        // Arrange
        var orderIds = new List<string> { "order-1", "order-2" };

        // Act
        await _sut.BulkDeleteOrdersAsync("store-1", orderIds);

        // Assert
        await _repository.Received(1).BulkDeleteOrdersAsync("store-1", orderIds);
    }

    // ──────────────────────────────────────────────
    // GetFullOrderByIdAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetFullOrderByIdAsync_OrderNotFound_ReturnsNulls()
    {
        // Arrange
        _repository.ReadAsync("store-1", "order-1").Returns((ChannelOrder?)null);

        // Act
        var (order, content) = await _sut.GetFullOrderByIdAsync("store-1", "order-1");

        // Assert
        Assert.Null(order);
        Assert.Null(content);
    }

    [Fact]
    public async Task GetFullOrderByIdAsync_OrderFoundWithS3Content_ReturnsBoth()
    {
        // Arrange
        var shipmentOrder = CreateShipmentOrder("order-1");
        _repository.ReadAsync("store-1", "order-1").Returns(shipmentOrder);

        var orderRequest = new AddShipmentOrderRequest
        {
            CustomerId = "c1",
            StoreId = "store-1",
            OrderFlow = OrderFlowType.INCOMING,
            Content = "The real content",
            OrderPlacedDate = DateTimeOffset.UtcNow,
            Merchant = new OrderHub.Contracts.Common.Merchant
            {
                Name = OrderHub.Contracts.Common.Enums.MerchantName.PRIME,
                OrderId = "source123"
            },
            FulfillmentStatus = OrderHub.Contracts.Common.Enums.FulfillmentStatus.SUCCESS,
            OrderFulfilledDate = DateTimeOffset.UtcNow,
            To = [new OrderHub.Contracts.Common.AddressInfo { Address = "a" }],
            From = new OrderHub.Contracts.Common.AddressInfo { Address = "b" }
        };

        _s3Service.GetObjectAsync<OrderRequest>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                Content = orderRequest,
                ErrorType = S3ErrorType.NONE
            });

        // Act
        var (order, content) = await _sut.GetFullOrderByIdAsync("store-1", "order-1");

        // Assert
        Assert.NotNull(order);
        Assert.Equal("The real content", content);
    }

    [Fact]
    public async Task GetFullOrderByIdAsync_S3Error_ReturnsOrderWithNullContent()
    {
        // Arrange
        var shipmentOrder = CreateShipmentOrder("order-1");
        _repository.ReadAsync("store-1", "order-1").Returns(shipmentOrder);

        _s3Service.GetObjectAsync<OrderRequest>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                ErrorType = S3ErrorType.UNEXPECTED,
                ErrorMessage = "S3 down"
            });

        // Act
        var (order, content) = await _sut.GetFullOrderByIdAsync("store-1", "order-1");

        // Assert
        Assert.NotNull(order);
        Assert.Null(content);
    }

    // ──────────────────────────────────────────────
    // GetOrderContentByEncodedKeyAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetOrderContentByEncodedKeyAsync_ValidKey_ReturnsContent()
    {
        // Arrange
        var key = "STANDARD/PRIME/STANDARD/source123/64f1234567890abcdef12345";
        var encodedKey = OrderHub.Common.Utilities.Base64UrlTextEncoderHelper.Encode(key);

        var orderRequest = new AddShipmentOrderRequest
        {
            CustomerId = "c1",
            StoreId = "store-1",
            OrderFlow = OrderFlowType.INCOMING,
            Content = "Order content here",
            OrderPlacedDate = DateTimeOffset.UtcNow,
            Merchant = new OrderHub.Contracts.Common.Merchant
            {
                Name = OrderHub.Contracts.Common.Enums.MerchantName.PRIME,
                OrderId = "source123"
            },
            FulfillmentStatus = OrderHub.Contracts.Common.Enums.FulfillmentStatus.SUCCESS,
            OrderFulfilledDate = DateTimeOffset.UtcNow,
            To = [new OrderHub.Contracts.Common.AddressInfo { Address = "a" }],
            From = new OrderHub.Contracts.Common.AddressInfo { Address = "b" }
        };

        _s3Service.GetObjectAsync<OrderRequest>(Arg.Any<string>(), key)
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                Content = orderRequest,
                ErrorType = S3ErrorType.NONE
            });

        // Act
        var result = await _sut.GetOrderContentByEncodedKeyAsync(encodedKey);

        // Assert
        Assert.Equal("Order content here", result);
    }

    [Fact]
    public async Task GetOrderContentByEncodedKeyAsync_S3NotFound_ReturnsNull()
    {
        // Arrange
        var key = "STANDARD/PRIME/STANDARD/source123/64f1234567890abcdef12345";
        var encodedKey = OrderHub.Common.Utilities.Base64UrlTextEncoderHelper.Encode(key);

        _s3Service.GetObjectAsync<OrderRequest>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                ErrorType = S3ErrorType.NOT_FOUND,
                ErrorMessage = "Not found"
            });

        // Act
        var result = await _sut.GetOrderContentByEncodedKeyAsync(encodedKey);

        // Assert
        Assert.Null(result);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static ShipmentOrder CreateShipmentOrder(string orderId)
    {
        return new ShipmentOrder
        {
            OrderId = orderId,
            CustomerId = "customer-1",
            StoreId = "store-1",
            OrderPlacedDate = DateTimeOffset.UtcNow,
            OrderFlow = (OrderHub.Common.Models.Components.OrderFlowType)OrderFlowType.INCOMING,
            Merchant = new Merchant
            {
                Name = MerchantName.PRIME,
                OrderId = "source123"
            },
            FulfillmentStatus = FulfillmentStatus.SUCCESS,
            Priority = Priority.STANDARD,
            CreatedDate = DateTimeOffset.UtcNow,
            UpdatedDate = DateTimeOffset.UtcNow,
            To = [new AddressInfo { Address = "addr1" }],
            From = new AddressInfo { Address = "addr2" }
        };
    }
}
