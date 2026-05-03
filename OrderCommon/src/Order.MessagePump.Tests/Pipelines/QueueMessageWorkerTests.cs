using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Order.MessagePump.Handlers;
using Order.MessagePump.Messages;
using Order.MessagePump.Pipelines;
using Order.MessagePump.Queues;
using Xunit;

namespace Order.MessagePump.Tests.Pipelines;

public class QueueMessageWorkerTests
{
    private readonly IQueueClient<TestMessage> _queue = Substitute.For<IQueueClient<TestMessage>>();
    private readonly IMessageHandler<TestMessage> _handler = Substitute.For<IMessageHandler<TestMessage>>();
    private readonly QueueMessageWorkerOptions _options;
    private readonly QueueMessageWorker<TestMessage> _sut;

    public QueueMessageWorkerTests()
    {
        _options = new QueueMessageWorkerOptions
        {
            MaxNumberOfMessages = 10,
            ExceptionsAllowedBeforeBreaking = 5,
            DurationOfBreakSeconds = 1
        };
        _sut = new QueueMessageWorker<TestMessage>(_options, _queue, _handler);
    }

    // ──────────────────────────────────────────────
    // GetMessagesAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetMessagesAsync_QueueReturnsMessages_ReturnsThem()
    {
        // Arrange
        var messages = new List<TestMessage> { new("msg-1"), new("msg-2") };
        _queue.GetMessagesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);

        // Act
        var result = await _sut.GetMessagesAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetMessagesAsync_QueueThrows_ReturnsEmptyList()
    {
        // Arrange
        _queue.GetMessagesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("queue error"));

        // Act
        var result = await _sut.GetMessagesAsync();

        // Assert
        Assert.Empty(result);
    }

    // ──────────────────────────────────────────────
    // ProcessMessageAsync — Complete routing
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessMessageAsync_HandlerReturnsComplete_CompletesOnQueue()
    {
        // Arrange
        var message = new TestMessage("msg-1");
        _handler.HandleMessageAsync(message, Arg.Any<CancellationToken>())
            .Returns(MessageResult.Complete());

        // Act
        await _sut.ProcessMessageAsync(message);

        // Assert
        await _queue.Received(1).CompleteMessageAsync(message, Arg.Any<CancellationToken>());
        await _queue.DidNotReceiveWithAnyArgs().PoisonMessageAsync(default!, default, default, default);
        await _queue.DidNotReceiveWithAnyArgs().RetryMessageAsync(default!, default, default);
    }

    // ──────────────────────────────────────────────
    // ProcessMessageAsync — Poison routing
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessMessageAsync_HandlerReturnsPoison_PoisonsOnQueue()
    {
        // Arrange
        var message = new TestMessage("msg-1");
        var ex = new InvalidOperationException("bad data");
        _handler.HandleMessageAsync(message, Arg.Any<CancellationToken>())
            .Returns(MessageResult.Poison(ex, "permanent error"));

        // Act
        await _sut.ProcessMessageAsync(message);

        // Assert
        await _queue.Received(1).PoisonMessageAsync(message, ex, "permanent error", Arg.Any<CancellationToken>());
        await _queue.DidNotReceiveWithAnyArgs().CompleteMessageAsync(default!, default);
    }

    // ──────────────────────────────────────────────
    // ProcessMessageAsync — Retry routing
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessMessageAsync_HandlerReturnsRetry_RetriesOnQueue()
    {
        // Arrange
        var message = new TestMessage("msg-1");
        var backoff = TimeSpan.FromSeconds(5);
        _handler.HandleMessageAsync(message, Arg.Any<CancellationToken>())
            .Returns(MessageResult.Retry(details: "transient", backoff: backoff));

        // Act
        await _sut.ProcessMessageAsync(message);

        // Assert
        await _queue.Received(1).RetryMessageAsync(message, backoff, Arg.Any<CancellationToken>());
        await _queue.DidNotReceiveWithAnyArgs().CompleteMessageAsync(default!, default);
        await _queue.DidNotReceiveWithAnyArgs().PoisonMessageAsync(default!, default, default, default);
    }

    // ──────────────────────────────────────────────
    // ProcessMessageAsync — handler exception
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessMessageAsync_HandlerThrows_DoesNotCrash()
    {
        // Arrange
        var message = new TestMessage("msg-1");
        _handler.HandleMessageAsync(message, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act — should not throw (caught internally)
        await _sut.ProcessMessageAsync(message);

        // Assert — no queue operations called since handler threw
        await _queue.DidNotReceiveWithAnyArgs().CompleteMessageAsync(default!, default);
    }

    // ──────────────────────────────────────────────
    // Test message type
    // ──────────────────────────────────────────────

    public record TestMessage(string Id);
}
