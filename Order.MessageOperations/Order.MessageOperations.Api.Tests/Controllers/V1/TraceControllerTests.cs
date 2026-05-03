using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models.Requests;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class TraceControllerTests
{
    private readonly ITraceService _traceService;
    private readonly TraceController _sut;

    public TraceControllerTests()
    {
        _traceService = Substitute.For<ITraceService>();
        _sut = new TraceController(_traceService);
    }

    // ── WaitForS3Object ───────────────────────────────────────────

    [Fact]
    public async Task WaitForS3Object_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new WaitForS3ObjectRequest
        {
            BucketName = "my-bucket",
            KeyPrefix = "orders/",
            TimeoutSeconds = 10,
            PollIntervalMs = 100
        };
        var expected = new TraceS3Result(
            Found: true, BucketName: "my-bucket", KeyPrefix: "orders/",
            ElapsedMs: 150, TimeoutMs: 10000, MatchedKey: "orders/123.json",
            Size: 1024, Detail: "Found after 150ms");

        _traceService.WaitForS3ObjectAsync("my-bucket", "orders/", 10, 100, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.WaitForS3Object(request, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TraceS3Result>(ok.Value);
        Assert.True(response.Found);
        Assert.Equal("orders/123.json", response.MatchedKey);
    }

    [Fact]
    public async Task WaitForS3Object_EmptyBucket_ReturnsBadRequest()
    {
        var request = new WaitForS3ObjectRequest { BucketName = "", KeyPrefix = "test/" };
        var result = await _sut.WaitForS3Object(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task WaitForS3Object_EmptyKeyPrefix_ReturnsBadRequest()
    {
        var request = new WaitForS3ObjectRequest { BucketName = "bucket", KeyPrefix = "" };
        var result = await _sut.WaitForS3Object(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── WaitForQueueMessage ───────────────────────────────────────

    [Fact]
    public async Task WaitForQueueMessage_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new WaitForQueueMessageRequest
        {
            QueueName = "order-queue",
            BodyContains = "order-123",
            TimeoutSeconds = 15,
            PollIntervalMs = 200
        };
        var expected = new TraceQueueResult(
            Found: true, QueueName: "order-queue", BodyContains: "order-123",
            ElapsedMs: 400, TimeoutMs: 15000, MessageId: "msg-abc",
            BodyPreview: "{\"orderId\":\"order-123\"}", Detail: "Found after 400ms");

        _traceService.WaitForQueueMessageAsync("order-queue", "order-123", 15, 200, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.WaitForQueueMessage(request, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TraceQueueResult>(ok.Value);
        Assert.True(response.Found);
        Assert.Equal("msg-abc", response.MessageId);
    }

    [Fact]
    public async Task WaitForQueueMessage_EmptyQueueName_ReturnsBadRequest()
    {
        var request = new WaitForQueueMessageRequest { QueueName = "" };
        var result = await _sut.WaitForQueueMessage(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── WaitForMongoDocument ──────────────────────────────────────

    [Fact]
    public async Task WaitForMongoDocument_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new WaitForMongoDocumentRequest
        {
            StoreId = "store-1",
            ProviderOrderId = "prov-123",
            TimeoutSeconds = 20
        };
        var expected = new TraceMongoResult(
            Found: true, StoreId: "store-1", ProviderOrderId: "prov-123", CustomerId: null,
            ElapsedMs: 300, TimeoutMs: 20000, MatchedOrderId: "64a1b2c3d4e5f6a7b8c9d0e1",
            Detail: "Found by providerOrderId after 300ms");

        _traceService.WaitForMongoDocumentAsync("store-1", "prov-123", null, 20, 500, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.WaitForMongoDocument(request, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TraceMongoResult>(ok.Value);
        Assert.True(response.Found);
        Assert.Equal("64a1b2c3d4e5f6a7b8c9d0e1", response.MatchedOrderId);
    }

    [Fact]
    public async Task WaitForMongoDocument_EmptyStoreId_ReturnsBadRequest()
    {
        var request = new WaitForMongoDocumentRequest { StoreId = "" };
        var result = await _sut.WaitForMongoDocument(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── GetAllQueueDepths ─────────────────────────────────────────

    [Fact]
    public async Task GetAllQueueDepths_ReturnsOkWithDepths()
    {
        // Arrange
        var expected = new AllQueueDepthsResult(
            Queues: new List<QueueDepthEntry>
            {
                new("inbound", "local-inbound", 5, 0),
                new("outbound", "local-outbound", 0, 0)
            },
            TotalMessages: 5);

        _traceService.GetAllQueueDepthsAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.GetAllQueueDepths(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AllQueueDepthsResult>(ok.Value);
        Assert.Equal(2, response.Queues.Count);
        Assert.Equal(5, response.TotalMessages);
    }
}
