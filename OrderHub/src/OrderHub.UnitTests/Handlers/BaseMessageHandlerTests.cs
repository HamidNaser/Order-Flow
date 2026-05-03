using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using NSubstitute;
using Order.MessagePump.Messages;
using OrderHub.Common.Configuration.Queues;
using OrderHub.Common.Handlers;
using OrderHub.Common.Models;
using OrderHub.Common.Telemetry;
using Xunit;

namespace OrderHub.UnitTests.Handlers;

public class BaseMessageHandlerTests
{
    private const int MaxRetries = 3;

    // ──────────────────────────────────────────────
    // Parsing failure → Poison
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ParsingFails_ReturnsPoisonResult()
    {
        // Arrange
        var handler = CreateHandler(parseResult: ParsingResult<string>.Failure("bad payload"));

        // Act
        var result = await handler.HandleMessageAsync(new Message { Body = "irrelevant" });

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    // ──────────────────────────────────────────────
    // Processing returns Complete
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ProcessPayloadReturnsComplete_ReturnsComplete()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            processResult: ProcessingResult.Complete()
        );

        // Act
        var result = await handler.HandleMessageAsync(new Message { Body = "test" });

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
    }

    // ──────────────────────────────────────────────
    // Processing returns Retry
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ProcessPayloadReturnsRetry_ReturnsRetryWithBackoff()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            processResult: ProcessingResult.Retry("transient error")
        );

        // Act
        var result = await handler.HandleMessageAsync(new Message { Body = "test" });

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
        Assert.NotNull(result.Backoff);
    }

    // ──────────────────────────────────────────────
    // Retry escalation → Poison after max retries
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_RetryExceedsMaxRetries_EscalatesToPoison()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            processResult: ProcessingResult.Retry("still failing")
        );

        var message = new Message
        {
            Body = "test",
            Attributes = new Dictionary<string, string>
            {
                ["ApproximateReceiveCount"] = "10" // exceeds max of 3
            }
        };

        // Act
        var result = await handler.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    // ──────────────────────────────────────────────
    // Retry with completeAfterMaxRetry → Complete after max retries
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_RetryFinalExceedsMaxRetries_CompletesInstead()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            processResult: ProcessingResult.Retry("transient", completeAfterMaxRetry: true)
        );

        var message = new Message
        {
            Body = "test",
            Attributes = new Dictionary<string, string>
            {
                ["ApproximateReceiveCount"] = "10"
            }
        };

        // Act
        var result = await handler.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
    }

    [Fact]
    public async Task HandleMessageAsync_RetryFinalBelowMaxRetries_StillRetries()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            processResult: ProcessingResult.Retry("transient", completeAfterMaxRetry: true)
        );

        var message = new Message
        {
            Body = "test",
            Attributes = new Dictionary<string, string>
            {
                ["ApproximateReceiveCount"] = "1"
            }
        };

        // Act
        var result = await handler.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
    }

    // ──────────────────────────────────────────────
    // Processing returns Poison
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ProcessPayloadReturnsPoison_ReturnsPoison()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            processResult: ProcessingResult.Poison("permanent error")
        );

        // Act
        var result = await handler.HandleMessageAsync(new Message { Body = "test" });

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    // ──────────────────────────────────────────────
    // Unhandled exception → Retry
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ProcessPayloadThrows_ReturnsRetry()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            throwOnProcess: new InvalidOperationException("unexpected error")
        );

        // Act
        var result = await handler.HandleMessageAsync(new Message { Body = "test" });

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
    }

    [Fact]
    public async Task HandleMessageAsync_ProcessPayloadThrowsExceedsMaxRetries_EscalatesToPoison()
    {
        // Arrange
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            throwOnProcess: new InvalidOperationException("unexpected error")
        );

        var message = new Message
        {
            Body = "test",
            Attributes = new Dictionary<string, string>
            {
                ["ApproximateReceiveCount"] = "10"
            }
        };

        // Act
        var result = await handler.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    // ──────────────────────────────────────────────
    // ReceiveCount parsing
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_NoReceiveCountAttribute_DefaultsToOne()
    {
        // Arrange — retry at receive count 1 (below max) should not poison
        var handler = CreateHandler(
            parseResult: ParsingResult<string>.Success("valid-payload"),
            processResult: ProcessingResult.Retry("retry me")
        );

        // Act — no Attributes set
        var result = await handler.HandleMessageAsync(new Message { Body = "test" });

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
    }

    // ──────────────────────────────────────────────
    // Test harness
    // ──────────────────────────────────────────────

    private static TestableMessageHandler CreateHandler(
        ParsingResult<string>? parseResult = null,
        ProcessingResult? processResult = null,
        Exception? throwOnProcess = null)
    {
        var options = Options.Create(new MessageHandlerOptions { MaxMessageRetries = MaxRetries });
        return new TestableMessageHandler(Substitute.For<IOrderMetrics>(), options, parseResult, processResult, throwOnProcess);
    }

    /// <summary>
    /// Concrete implementation of BaseMessageHandler for testing the base class logic.
    /// </summary>
    private sealed class TestableMessageHandler(
        IOrderMetrics metrics,
        IOptions<MessageHandlerOptions> options,
        ParsingResult<string>? parseResult,
        ProcessingResult? processResult,
        Exception? throwOnProcess)
        : BaseMessageHandler<string>(metrics, options)
    {
        protected override string MessageType => "Test";

        protected override ParsingResult<string> ParsePayload(Message message)
        {
            return parseResult ?? ParsingResult<string>.Success(message.Body);
        }

        protected override Task<ProcessingResult> ProcessPayload(string payload, CancellationToken cancellationToken)
        {
            if (throwOnProcess != null) throw throwOnProcess;
            return Task.FromResult(processResult ?? ProcessingResult.Complete());
        }

        protected override DisposableList CreateLogContext(string payload)
        {
            return [];
        }
    }
}
