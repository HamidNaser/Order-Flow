using System.Text;
using System.Text.Json;
using Order.MessageOperations.Api.Services;

namespace Order.MessageOperations.Api.Tests.Services;

public class TestDataServiceTests
{
    private readonly TestDataService _sut = new();

    [Fact]
    public void GenerateOrders_DefaultParams_ReturnsSingleGatewayOrder()
    {
        var result = _sut.GenerateOrders();

        Assert.Single(result);
        var order = result[0];
        Assert.Equal(1, order.Index);
        Assert.Equal("STANDARD", order.Priority);
        Assert.Equal("STANDARD", order.ChannelType);
        Assert.Equal("gateway", order.Format);
        Assert.Equal("order-gateway-incoming", order.TargetQueue);
        Assert.NotEmpty(order.OrderReferenceId);
        Assert.NotEmpty(order.Body);
        Assert.NotEmpty(order.Description);
    }

    [Fact]
    public void GenerateOrders_GatewayFormat_ReturnsBase64EncodedBody()
    {
        var result = _sut.GenerateOrders(format: "gateway");

        var order = result[0];
        // Body should be valid base64
        var decoded = Convert.FromBase64String(order.Body);
        var json = Encoding.UTF8.GetString(decoded);

        // Should be valid JSON with expected fields
        var doc = JsonDocument.Parse(json);
        Assert.Equal("Order", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Outbound order", doc.RootElement.GetProperty("subType").GetString());
        Assert.True(doc.RootElement.TryGetProperty("metadata", out var metadata));
        Assert.True(metadata.TryGetProperty("Classification", out _));
        Assert.True(metadata.TryGetProperty("StoreId", out _));
        Assert.True(metadata.TryGetProperty("OrderReferenceId", out _));
    }

    [Fact]
    public void GenerateOrders_StandardPriority_GatewayFormat_UsesBatchClassification()
    {
        var result = _sut.GenerateOrders(priority: "standard", format: "gateway");

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(result[0].Body));
        var doc = JsonDocument.Parse(decoded);
        var classification = doc.RootElement
            .GetProperty("metadata")
            .GetProperty("Classification")
            .GetString();
        Assert.Equal("batch", classification);
    }

    [Fact]
    public void GenerateOrders_ExpressPriority_GatewayFormat_UsesManualOrderClassification()
    {
        var result = _sut.GenerateOrders(priority: "express", format: "gateway");

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(result[0].Body));
        var doc = JsonDocument.Parse(decoded);
        var classification = doc.RootElement
            .GetProperty("metadata")
            .GetProperty("Classification")
            .GetString();
        Assert.Equal("ManualOrder", classification);
    }

    [Fact]
    public void GenerateOrders_IngestStandardFormat_ReturnsShipmentJson()
    {
        var result = _sut.GenerateOrders(format: "ingest", channelType: "STANDARD");

        var order = result[0];
        Assert.Equal("ingest", order.Format);

        // Body should be valid JSON (not base64)
        var doc = JsonDocument.Parse(order.Body);
        Assert.Equal("STANDARD", doc.RootElement.GetProperty("channelType").GetString());
        Assert.True(doc.RootElement.TryGetProperty("to", out _));
        Assert.True(doc.RootElement.TryGetProperty("from", out _));
        Assert.True(doc.RootElement.TryGetProperty("merchant", out _));
        Assert.True(doc.RootElement.TryGetProperty("fulfillmentStatus", out _));
    }

    [Fact]
    public void GenerateOrders_IngestDigitalFormat_ReturnsDigitalJson()
    {
        var result = _sut.GenerateOrders(format: "ingest", channelType: "DIGITAL");

        var doc = JsonDocument.Parse(result[0].Body);
        Assert.Equal("DIGITAL", doc.RootElement.GetProperty("channelType").GetString());
        Assert.True(doc.RootElement.TryGetProperty("toPhoneNumber", out _));
        Assert.True(doc.RootElement.TryGetProperty("fromPhoneNumber", out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void GenerateOrders_MultipleCount_ReturnsRequestedNumber(int count)
    {
        var result = _sut.GenerateOrders(count: count);

        Assert.Equal(count, result.Count);
        // Each order should have sequential index
        for (var i = 0; i < count; i++)
            Assert.Equal(i + 1, result[i].Index);
    }

    [Fact]
    public void GenerateOrders_EachOrderGetsUniqueReferenceId()
    {
        var result = _sut.GenerateOrders(count: 10);

        var ids = result.Select(o => o.OrderReferenceId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void GenerateOrders_WithStoreId_AllOrdersUseIt()
    {
        var result = _sut.GenerateOrders(count: 3, storeId: "77777");

        Assert.All(result, o => Assert.Equal("77777", o.StoreId));
    }

    [Fact]
    public void GenerateOrders_WithoutStoreId_AllOrdersShareSameStoreId()
    {
        var result = _sut.GenerateOrders(count: 5);

        // All orders in a batch should get the same random store ID
        var storeIds = result.Select(o => o.StoreId).Distinct().ToList();
        Assert.Single(storeIds);
    }

    [Fact]
    public void GenerateOrders_ExpressPriority_SetsExpressOnOrders()
    {
        var result = _sut.GenerateOrders(priority: "express", count: 3);

        Assert.All(result, o => Assert.Equal("EXPRESS", o.Priority));
    }

    [Fact]
    public void GenerateOrders_GatewayFormat_TargetQueueIsGateway()
    {
        var result = _sut.GenerateOrders(format: "gateway", priority: "standard");
        Assert.All(result, o => Assert.Equal("order-gateway-incoming", o.TargetQueue));
    }

    [Fact]
    public void GenerateOrders_IngestFormatStandard_TargetQueueIsStandard()
    {
        var result = _sut.GenerateOrders(format: "ingest", priority: "standard");
        Assert.All(result, o => Assert.Equal("order-hub-standard-order", o.TargetQueue));
    }

    [Fact]
    public void GenerateOrders_IngestFormatExpress_TargetQueueIsExpress()
    {
        var result = _sut.GenerateOrders(format: "ingest", priority: "express");
        Assert.All(result, o => Assert.Equal("order-hub-express-order", o.TargetQueue));
    }

    [Fact]
    public void GenerateOrders_DescriptionContainsPriorityAndStoreId()
    {
        var result = _sut.GenerateOrders(priority: "express", storeId: "12345");

        Assert.All(result, o =>
        {
            Assert.Contains("EXPRESS", o.Description);
            Assert.Contains("12345", o.Description);
        });
    }

    [Fact]
    public void GenerateOrders_GatewayFormat_MetadataHasStoreId()
    {
        var result = _sut.GenerateOrders(format: "gateway", storeId: "55555");

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(result[0].Body));
        var doc = JsonDocument.Parse(decoded);
        var storeId = doc.RootElement
            .GetProperty("metadata")
            .GetProperty("StoreId")
            .GetString();
        Assert.Equal("55555", storeId);
    }
}
