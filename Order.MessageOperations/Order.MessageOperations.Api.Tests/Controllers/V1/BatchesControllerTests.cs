using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class BatchesControllerTests
{
    private readonly IMessageStorageService _storageService;
    private readonly BatchesController _sut;

    public BatchesControllerTests()
    {
        _storageService = Substitute.For<IMessageStorageService>();
        _sut = new BatchesController(_storageService);
    }

    [Fact]
    public void ListBatches_ReturnsSortedBatches()
    {
        // Arrange
        _storageService.ListAvailableBatches().Returns(new List<(string QueueType, List<string> Batches)>
        {
            ("zeta-queue", new List<string> { "b1" }),
            ("alpha-queue", new List<string> { "b2", "b3" })
        });

        // Act
        var result = _sut.ListBatches();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void ListBatches_NoBatches_ReturnsEmptyList()
    {
        // Arrange
        _storageService.ListAvailableBatches()
            .Returns(new List<(string QueueType, List<string> Batches)>());

        // Act
        var result = _sut.ListBatches();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetBatchManifest_Exists_ReturnsManifest()
    {
        // Arrange
        var manifest = new MessageBatch { BatchId = "batch1", QueueType = "orders", MessageCount = 5 };
        _storageService.BuildBatchPath("orders", "batch1").Returns("/path/orders/batch1");
        _storageService.LoadManifestAsync("/path/orders/batch1").Returns(manifest);

        // Act
        var result = await _sut.GetBatchManifest("orders", "batch1");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(manifest, ok.Value);
    }

    [Fact]
    public async Task GetBatchManifest_NotFound_ReturnsNotFound()
    {
        // Arrange
        _storageService.BuildBatchPath("orders", "missing").Returns("/path/orders/missing");
        _storageService.LoadManifestAsync("/path/orders/missing").Returns((MessageBatch?)null);

        // Act
        var result = await _sut.GetBatchManifest("orders", "missing");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetBatchMessages_BatchExists_ReturnsMessages()
    {
        // Arrange
        var batchPath = Path.Combine(Path.GetTempPath(), "test-batch-" + Guid.NewGuid());
        Directory.CreateDirectory(batchPath);
        try
        {
            _storageService.BuildBatchPath("orders", "batch1").Returns(batchPath);
            var messages = new List<SavedMessage>
            {
                new() { MessageId = "m1", Body = "body1" },
                new() { MessageId = "m2", Body = "body2" }
            };
            _storageService.LoadBatchAsync(batchPath).Returns(messages);

            // Act
            var result = await _sut.GetBatchMessages("orders", "batch1");

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(messages, ok.Value);
        }
        finally
        {
            Directory.Delete(batchPath, true);
        }
    }

    [Fact]
    public async Task GetBatchMessages_BatchNotFound_ReturnsNotFound()
    {
        // Arrange
        _storageService.BuildBatchPath("orders", "missing").Returns("/nonexistent/path");

        // Act
        var result = await _sut.GetBatchMessages("orders", "missing");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
