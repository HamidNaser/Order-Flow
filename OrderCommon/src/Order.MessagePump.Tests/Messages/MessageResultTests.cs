using Order.MessagePump.Messages;
using Xunit;

namespace Order.MessagePump.Tests.Messages;

public class MessageResultTests
{
    // ──────────────────────────────────────────────
    // Factory methods
    // ──────────────────────────────────────────────

    [Fact]
    public void Complete_ReturnsCompleteAction()
    {
        // Act
        var result = MessageResult.Complete();

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Null(result.Details);
        Assert.Null(result.Exception);
        Assert.Null(result.Backoff);
    }

    [Fact]
    public void Complete_WithDetails_ReturnsCompleteWithDetails()
    {
        // Act
        var result = MessageResult.Complete("done");

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Equal("done", result.Details);
    }

    [Fact]
    public void Retry_ReturnsRetryAction()
    {
        // Act
        var result = MessageResult.Retry(details: "transient error");

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
        Assert.Equal("transient error", result.Details);
    }

    [Fact]
    public void Retry_WithExceptionAndBackoff_SetsAllProperties()
    {
        // Arrange
        var ex = new InvalidOperationException("oops");
        var backoff = TimeSpan.FromSeconds(30);

        // Act
        var result = MessageResult.Retry(ex, "retry reason", backoff);

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
        Assert.Equal("retry reason", result.Details);
        Assert.Equal(ex, result.Exception);
        Assert.Equal(backoff, result.Backoff);
    }

    [Fact]
    public void Poison_ReturnsPoisonAction()
    {
        // Act
        var result = MessageResult.Poison(reason: "bad data");

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
        Assert.Equal("bad data", result.Details);
    }

    [Fact]
    public void Poison_WithException_SetsException()
    {
        // Arrange
        var ex = new FormatException("invalid format");

        // Act
        var result = MessageResult.Poison(ex, "permanent error");

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
        Assert.Equal(ex, result.Exception);
        Assert.Equal("permanent error", result.Details);
    }

    // ──────────────────────────────────────────────
    // WithBackoff
    // ──────────────────────────────────────────────

    [Fact]
    public void WithBackoff_ReturnsNewResultWithBackoff()
    {
        // Arrange
        var original = MessageResult.Retry(details: "retry");
        var backoff = TimeSpan.FromSeconds(10);

        // Act
        var updated = original.WithBackoff(backoff);

        // Assert
        Assert.Equal(MessageResultAction.Retry, updated.Action);
        Assert.Equal("retry", updated.Details);
        Assert.Equal(backoff, updated.Backoff);
    }

    [Fact]
    public void WithBackoff_PreservesAllOtherProperties()
    {
        // Arrange
        var ex = new InvalidOperationException("err");
        var original = MessageResult.Retry(ex, "details");

        // Act
        var updated = original.WithBackoff(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(ex, updated.Exception);
        Assert.Equal("details", updated.Details);
        Assert.Equal(MessageResultAction.Retry, updated.Action);
    }
}
