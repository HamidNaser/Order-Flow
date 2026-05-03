using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ApiSendMessageRequest = Order.MessageOperations.Api.Models.Requests.SendMessageRequest;
using ApiSendMessageResponse = Order.MessageOperations.Api.Models.Responses.SendMessageResponse;
using ApiPurgeQueueResponse = Order.MessageOperations.Api.Models.Responses.PurgeQueueResponse;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class QueuesControllerTests
{
    private readonly IQueueReplayService _queueReplayService;
    private readonly QueuesController _sut;

    public QueuesControllerTests()
    {
        var options = Options.Create(new MessageOperationsOptions
        {
            Queues = new Dictionary<string, QueueMappingOptions>
            {
                ["inbound"] = new()
                {
                    DisplayName = "Inbound Orders",
                    LocalStackQueueName = "local-inbound",
                    AwsDlqName = "aws-inbound-dlq",
                    AwsSourceQueueName = "aws-inbound",
                    Enabled = true
                },
                ["outbound"] = new()
                {
                    DisplayName = "Outbound Orders",
                    LocalStackQueueName = "local-outbound",
                    AwsDlqName = "aws-outbound-dlq",
                    Enabled = false
                }
            }
        });

        _queueReplayService = Substitute.For<IQueueReplayService>();
        _sut = new QueuesController(options, _queueReplayService);
    }

    [Fact]
    public void GetConfiguredQueues_ReturnsSortedQueues()
    {
        // Act
        var result = _sut.GetConfiguredQueues();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task ListQueues_LocalStack_CallsListQueuesWithTrue()
    {
        // Arrange
        _queueReplayService.ListQueuesAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<string> { "queue-b", "queue-a" });

        // Act
        var result = await _sut.ListQueues("localstack", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<List<string>>(ok.Value);
        Assert.Equal("queue-a", list[0]); // sorted
    }

    [Fact]
    public async Task ListQueues_Aws_CallsListQueuesWithFalse()
    {
        // Arrange
        _queueReplayService.ListQueuesAsync(false, Arg.Any<CancellationToken>())
            .Returns(new List<string> { "aws-queue" });

        // Act
        var result = await _sut.ListQueues("aws", CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        await _queueReplayService.Received(1).ListQueuesAsync(false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListLocalStackQueues_ReturnsQueues()
    {
        // Arrange
        _queueReplayService.ListLocalStackQueuesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "queue-1" });

        // Act
        var result = await _sut.ListLocalStackQueues(CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetQueueStatus_ReturnsAttributes()
    {
        // Arrange
        var attrs = new Dictionary<string, string>
        {
            ["ApproximateNumberOfMessages"] = "5",
            ["ApproximateNumberOfMessagesNotVisible"] = "0"
        };
        _queueReplayService.GetQueueAttributesAsync("my-queue", true, Arg.Any<CancellationToken>())
            .Returns(attrs);

        // Act
        var result = await _sut.GetQueueStatus("my-queue", "localstack", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(attrs, ok.Value);
    }

    [Fact]
    public async Task PeekMessages_ReturnsProjectedMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                MessageId = "msg-1",
                Body = "test body",
                Attributes = new Dictionary<string, string> { ["attr"] = "val" },
                MessageAttributes = new Dictionary<string, MessageAttributeValue>()
            }
        };
        _queueReplayService.PeekMessagesAsync("my-queue", 5, true, Arg.Any<CancellationToken>())
            .Returns(messages);

        // Act
        var result = await _sut.PeekMessages("my-queue", 5, "localstack", CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    // ── SendMessage Tests ─────────────────────────────────────────

    [Fact]
    public async Task SendMessage_ValidRequest_ReturnsOkWithMessageId()
    {
        // Arrange
        var request = new ApiSendMessageRequest { Body = "{\"orderId\":\"123\"}" };
        _queueReplayService.SendMessageToLocalStackAsync(
                "my-queue", request.Body, null, null, Arg.Any<CancellationToken>())
            .Returns("msg-abc-123");

        // Act
        var result = await _sut.SendMessage("my-queue", request, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiSendMessageResponse>(ok.Value);
        Assert.Equal("my-queue", response.QueueName);
        Assert.Equal("msg-abc-123", response.MessageId);
    }

    [Fact]
    public async Task SendMessage_EmptyQueueName_ReturnsBadRequest()
    {
        // Arrange
        var request = new ApiSendMessageRequest { Body = "test" };

        // Act
        var result = await _sut.SendMessage("  ", request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SendMessage_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        var request = new ApiSendMessageRequest { Body = "" };

        // Act
        var result = await _sut.SendMessage("my-queue", request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── PurgeQueue Tests ──────────────────────────────────────────

    [Fact]
    public async Task PurgeQueue_ValidQueue_ReturnsOkWithSuccess()
    {
        // Act
        var result = await _sut.PurgeQueue("my-queue", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiPurgeQueueResponse>(ok.Value);
        Assert.Equal("my-queue", response.QueueName);
        Assert.True(response.Success);
        await _queueReplayService.Received(1).PurgeLocalStackQueueAsync("my-queue", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeQueue_EmptyQueueName_ReturnsBadRequest()
    {
        // Act
        var result = await _sut.PurgeQueue("  ", CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── PurgeAllQueues Tests ──────────────────────────────────────

    [Fact]
    public async Task PurgeAllQueues_ReturnsAggregatedResults()
    {
        // Arrange
        var results = new Dictionary<string, bool>
        {
            ["local-inbound"] = true,
            ["local-inbound-dlq"] = true,
            ["local-outbound"] = false
        };
        _queueReplayService.PurgeAllConfiguredLocalStackQueuesAsync(Arg.Any<CancellationToken>())
            .Returns(results);

        // Act
        var result = await _sut.PurgeAllQueues(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PurgeAllQueuesResponse>(ok.Value);
        Assert.Equal(2, response.Purged);
        Assert.Equal(1, response.Failed);
        Assert.Equal(3, response.Results.Count);
    }
}
