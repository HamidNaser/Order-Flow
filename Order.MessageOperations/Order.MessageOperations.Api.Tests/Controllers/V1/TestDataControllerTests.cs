using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class TestDataControllerTests
{
    private readonly ITestDataService _testDataService;
    private readonly TestDataController _sut;

    public TestDataControllerTests()
    {
        _testDataService = Substitute.For<ITestDataService>();
        _sut = new TestDataController(_testDataService);
    }

    [Fact]
    public void GenerateOrders_DefaultParams_ReturnsOk()
    {
        // Arrange
        var orders = new List<GeneratedOrder>
        {
            new(1, "ref-1", "10001", "STANDARD", "STANDARD", "gateway",
                "order-gateway-incoming", "body1", "desc1")
        };
        _testDataService.GenerateOrders("standard", "STANDARD", 1, null, "gateway")
            .Returns(orders);

        // Act
        var result = _sut.GenerateOrders();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GenerateOrdersResponse>(ok.Value);
        Assert.Equal(1, response.Count);
        Assert.Equal("STANDARD", response.Priority);
        Assert.Equal("gateway", response.Format);
        Assert.Single(response.Orders);
    }

    [Fact]
    public void GenerateOrders_Express5Orders_ReturnsCorrectCount()
    {
        // Arrange
        var orders = Enumerable.Range(1, 5).Select(i =>
            new GeneratedOrder(i, $"ref-{i}", "20001", "EXPRESS", "STANDARD", "gateway",
                "order-gateway-incoming", $"body{i}", $"desc{i}")).ToList();
        _testDataService.GenerateOrders("express", "STANDARD", 5, "20001", "gateway")
            .Returns(orders);

        // Act
        var result = _sut.GenerateOrders(priority: "express", count: 5, storeId: "20001");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GenerateOrdersResponse>(ok.Value);
        Assert.Equal(5, response.Count);
        Assert.Equal("EXPRESS", response.Priority);
        Assert.Equal(5, response.Orders.Count);
    }

    [Fact]
    public void GenerateOrders_IngestFormat_PassesFormatThrough()
    {
        // Arrange
        var orders = new List<GeneratedOrder>
        {
            new(1, "ref-1", "10001", "STANDARD", "DIGITAL", "ingest",
                "order-hub-standard-order", "body1", "desc1")
        };
        _testDataService.GenerateOrders("standard", "DIGITAL", 1, null, "ingest")
            .Returns(orders);

        // Act
        var result = _sut.GenerateOrders(channelType: "DIGITAL", format: "ingest");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GenerateOrdersResponse>(ok.Value);
        Assert.Equal("ingest", response.Format);
        Assert.Equal("DIGITAL", response.ChannelType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(100)]
    public void GenerateOrders_InvalidCount_ReturnsBadRequest(int count)
    {
        // Act
        var result = _sut.GenerateOrders(count: count);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(bad.Value);
        Assert.Contains("Count must be between 1 and 50", error.Message);
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData("low")]
    [InlineData("")]
    public void GenerateOrders_InvalidPriority_ReturnsBadRequest(string priority)
    {
        // Act
        var result = _sut.GenerateOrders(priority: priority);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(bad.Value);
        Assert.Contains("Priority must be", error.Message);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("json")]
    [InlineData("")]
    public void GenerateOrders_InvalidFormat_ReturnsBadRequest(string format)
    {
        // Act
        var result = _sut.GenerateOrders(format: format);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(bad.Value);
        Assert.Contains("Format must be", error.Message);
    }

    [Fact]
    public void GenerateOrders_MaxCount_ReturnsOk()
    {
        // Arrange
        var orders = Enumerable.Range(1, 50).Select(i =>
            new GeneratedOrder(i, $"ref-{i}", "10001", "STANDARD", "STANDARD", "gateway",
                "order-gateway-incoming", $"body{i}", $"desc{i}")).ToList();
        _testDataService.GenerateOrders("standard", "STANDARD", 50, null, "gateway")
            .Returns(orders);

        // Act
        var result = _sut.GenerateOrders(count: 50);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GenerateOrdersResponse>(ok.Value);
        Assert.Equal(50, response.Count);
    }

    [Fact]
    public void GenerateOrders_WithStoreIdOverride_PassesToService()
    {
        // Arrange
        var orders = new List<GeneratedOrder>
        {
            new(1, "ref-1", "99999", "STANDARD", "STANDARD", "gateway",
                "order-gateway-incoming", "body1", "desc1")
        };
        _testDataService.GenerateOrders("standard", "STANDARD", 1, "99999", "gateway")
            .Returns(orders);

        // Act
        var result = _sut.GenerateOrders(storeId: "99999");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GenerateOrdersResponse>(ok.Value);
        Assert.Equal("99999", response.Orders[0].StoreId);
    }

    [Fact]
    public void GenerateOrders_TargetQueueFromOrders_SetsOnResponse()
    {
        // Arrange
        var orders = new List<GeneratedOrder>
        {
            new(1, "ref-1", "10001", "EXPRESS", "STANDARD", "gateway",
                "order-gateway-incoming", "body1", "desc1")
        };
        _testDataService.GenerateOrders("express", "STANDARD", 1, null, "gateway")
            .Returns(orders);

        // Act
        var result = _sut.GenerateOrders(priority: "express");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GenerateOrdersResponse>(ok.Value);
        Assert.Equal("order-gateway-incoming", response.TargetQueue);
    }
}
