using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class OrdersControllerTests
{
    private readonly IOrderQueryService _queryService;
    private readonly OrdersController _sut;

    public OrdersControllerTests()
    {
        _queryService = Substitute.For<IOrderQueryService>();
        _sut = new OrdersController(_queryService);
    }

    [Fact]
    public async Task GetById_OrderExists_ReturnsOk()
    {
        // Arrange
        var record = new OrderRecord { StoreId = "store1", OrderId = "order1" };
        _queryService.GetByIdAsync("store1", "order1", Arg.Any<CancellationToken>())
            .Returns(record);

        // Act
        var result = await _sut.GetById("store1", "order1", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(record, ok.Value);
    }

    [Fact]
    public async Task GetById_OrderNotFound_ReturnsNotFound()
    {
        // Arrange
        _queryService.GetByIdAsync("store1", "missing", Arg.Any<CancellationToken>())
            .Returns((OrderRecord?)null);

        // Act
        var result = await _sut.GetById("store1", "missing", CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetByCustomer_ReturnsPagedResults()
    {
        // Arrange
        var records = new List<OrderRecord>
        {
            new() { OrderId = "o1" },
            new() { OrderId = "o2" }
        };
        _queryService.GetByCustomerAsync("store1", "cust1", 10, 0, Arg.Any<CancellationToken>())
            .Returns(records);
        _queryService.CountByCustomerAsync("store1", "cust1", Arg.Any<CancellationToken>())
            .Returns(25L);

        // Act
        var result = await _sut.GetByCustomer("store1", "cust1", 10, 0, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task CountByCustomer_ReturnsCount()
    {
        // Arrange
        _queryService.CountByCustomerAsync("store1", "cust1", Arg.Any<CancellationToken>())
            .Returns(42L);

        // Act
        var result = await _sut.CountByCustomer("store1", "cust1", CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Search_ReturnsMatchingResults()
    {
        // Arrange
        var records = new List<OrderRecord> { new() { OrderId = "o1" } };
        _queryService.SearchAsync("store1", Arg.Any<OrderSearchParams>(), Arg.Any<CancellationToken>())
            .Returns(records);

        // Act
        var result = await _sut.Search("store1", customerId: "cust1", ct: CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetSummary_ReturnsSummary()
    {
        // Arrange
        var summary = new OrderSummary { StoreId = "store1", TotalCount = 100 };
        _queryService.GetSummaryAsync("store1", Arg.Any<CancellationToken>())
            .Returns(summary);

        // Act
        var result = await _sut.GetSummary("store1", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(summary, ok.Value);
    }

    [Fact]
    public async Task FindByProvider_Exists_ReturnsOk()
    {
        // Arrange
        var record = new OrderRecord { OrderId = "o1" };
        _queryService.FindByProviderAsync("store1", "prov-id", "provName", null, Arg.Any<CancellationToken>())
            .Returns(record);

        // Act
        var result = await _sut.FindByProvider("store1", "provName", "prov-id", ct: CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(record, ok.Value);
    }

    [Fact]
    public async Task FindByProvider_NotFound_ReturnsNotFound()
    {
        // Arrange
        _queryService.FindByProviderAsync("store1", "prov-id", "provName", null, Arg.Any<CancellationToken>())
            .Returns((OrderRecord?)null);

        // Act
        var result = await _sut.FindByProvider("store1", "provName", "prov-id", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetRecent_ReturnsOrderedResults()
    {
        // Arrange
        var records = new List<OrderRecord> { new() { OrderId = "o1" } };
        _queryService.GetRecentAsync("store1", 20, Arg.Any<CancellationToken>())
            .Returns(records);

        // Act
        var result = await _sut.GetRecent("store1", ct: CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}
