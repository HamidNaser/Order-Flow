using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models;
using Order.MessageOperations.Api.Models.Requests;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class ReplayControllerTests
{
    private readonly IQueueReplayService _queueReplayService;
    private readonly IMessageStorageService _storageService;
    private readonly MessageOperationsOptions _optionsValue;
    private readonly ReplayController _sut;

    public ReplayControllerTests()
    {
        _optionsValue = new MessageOperationsOptions
        {
            Queues = new Dictionary<string, QueueMappingOptions>
            {
                ["inbound"] = new()
                {
                    DisplayName = "Inbound",
                    LocalStackQueueName = "local-inbound",
                    AwsDlqName = "aws-inbound-dlq",
                    AwsSourceQueueName = "aws-inbound"
                }
            }
        };

        _queueReplayService = Substitute.For<IQueueReplayService>();
        _storageService = Substitute.For<IMessageStorageService>();
        _sut = new ReplayController(
            Options.Create(_optionsValue),
            _queueReplayService,
            _storageService);
    }

    // --- DownloadMessages ---

    [Fact]
    public async Task DownloadMessages_EmptyQueueKey_ReturnsBadRequest()
    {
        // Arrange
        var request = new DownloadMessagesRequest { QueueKey = "" };

        // Act
        var result = await _sut.DownloadMessages(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DownloadMessages_UnknownQueueKey_ReturnsNotFound()
    {
        // Arrange
        var request = new DownloadMessagesRequest { QueueKey = "unknown" };

        // Act
        var result = await _sut.DownloadMessages(request, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DownloadMessages_ValidKey_DownloadsAndReturnsOk()
    {
        // Arrange
        var request = new DownloadMessagesRequest { QueueKey = "inbound", MaxMessages = 10 };
        _queueReplayService.DownloadFromAwsQueueAsyncByName(
            "inbound", "aws-inbound", 10, null, Arg.Any<CancellationToken>())
            .Returns((5, "/path/batch"));

        // Act
        var result = await _sut.DownloadMessages(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DownloadMessages_CustomAwsQueueName_UsesProvidedName()
    {
        // Arrange
        var request = new DownloadMessagesRequest
        {
            QueueKey = "inbound",
            AwsQueueName = "custom-queue"
        };
        _queueReplayService.DownloadFromAwsQueueAsyncByName(
            "inbound", "custom-queue", null, null, Arg.Any<CancellationToken>())
            .Returns((3, "/path/batch"));

        // Act
        var result = await _sut.DownloadMessages(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        await _queueReplayService.Received(1).DownloadFromAwsQueueAsyncByName(
            "inbound", "custom-queue", null, null, Arg.Any<CancellationToken>());
    }

    // --- ReplayFromBatch ---

    [Fact]
    public async Task ReplayFromBatch_MissingFields_ReturnsBadRequest()
    {
        // Arrange
        var request = new ReplayBatchRequest { QueueType = "", BatchId = "" };

        // Act
        var result = await _sut.ReplayFromBatch(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ReplayFromBatch_BatchNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new ReplayBatchRequest { QueueType = "inbound", BatchId = "missing" };
        _storageService.BuildBatchPath("inbound", "missing").Returns("/nonexistent/path");

        // Act
        var result = await _sut.ReplayFromBatch(request, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReplayFromBatch_EmptyBatch_ReturnsZero()
    {
        // Arrange
        var batchPath = Path.Combine(Path.GetTempPath(), "test-replay-" + Guid.NewGuid());
        Directory.CreateDirectory(batchPath);
        try
        {
            var request = new ReplayBatchRequest { QueueType = "inbound", BatchId = "empty" };
            _storageService.BuildBatchPath("inbound", "empty").Returns(batchPath);
            _storageService.LoadBatchAsync(batchPath).Returns(new List<SavedMessage>());

            // Act
            var result = await _sut.ReplayFromBatch(request, CancellationToken.None);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }
        finally
        {
            Directory.Delete(batchPath, true);
        }
    }

    [Fact]
    public async Task ReplayFromBatch_ValidBatch_ReplaysToLocalStack()
    {
        // Arrange
        var batchPath = Path.Combine(Path.GetTempPath(), "test-replay-" + Guid.NewGuid());
        Directory.CreateDirectory(batchPath);
        try
        {
            var request = new ReplayBatchRequest { QueueType = "inbound", BatchId = "batch1" };
            var messages = new List<SavedMessage> { new() { MessageId = "m1", Body = "body" } };
            _storageService.BuildBatchPath("inbound", "batch1").Returns(batchPath);
            _storageService.LoadBatchAsync(batchPath).Returns(messages);
            _queueReplayService.ReplayToLocalStackAsyncByName("local-inbound", messages, Arg.Any<CancellationToken>())
                .Returns(1);

            // Act
            var result = await _sut.ReplayFromBatch(request, CancellationToken.None);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            await _queueReplayService.Received(1).ReplayToLocalStackAsyncByName(
                "local-inbound", messages, Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(batchPath, true);
        }
    }

    [Fact]
    public async Task ReplayFromBatch_NoQueueMapping_NoLocalStackName_ReturnsBadRequest()
    {
        // Arrange
        var batchPath = Path.Combine(Path.GetTempPath(), "test-replay-" + Guid.NewGuid());
        Directory.CreateDirectory(batchPath);
        try
        {
            var request = new ReplayBatchRequest { QueueType = "unknown-type", BatchId = "batch1" };
            var messages = new List<SavedMessage> { new() { MessageId = "m1", Body = "body" } };
            _storageService.BuildBatchPath("unknown-type", "batch1").Returns(batchPath);
            _storageService.LoadBatchAsync(batchPath).Returns(messages);

            // Act
            var result = await _sut.ReplayFromBatch(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
        finally
        {
            Directory.Delete(batchPath, true);
        }
    }

    // --- DownloadAndReplay ---

    [Fact]
    public async Task DownloadAndReplay_EmptyQueueKey_ReturnsBadRequest()
    {
        // Arrange
        var request = new DownloadAndReplayRequest { QueueKey = "" };

        // Act
        var result = await _sut.DownloadAndReplay(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DownloadAndReplay_ValidKey_ReturnsReplayedCount()
    {
        // Arrange
        var request = new DownloadAndReplayRequest { QueueKey = "inbound", MaxMessages = 5 };
        _queueReplayService.DownloadAndReplayAsync("inbound", 5, null, Arg.Any<CancellationToken>())
            .Returns(3);

        // Act
        var result = await _sut.DownloadAndReplay(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}
