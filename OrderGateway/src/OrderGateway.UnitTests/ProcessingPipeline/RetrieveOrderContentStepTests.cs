using Order.MessagePump.Messages;
using OrderGateway.Common.Telemetry;
using NSubstitute;
using Xunit;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Processing.Steps;
using OrderGateway.Common.Services;

namespace OrderGateway.UnitTests.ProcessingPipeline;

public class RetrieveOrderContentStepTests
{
    private readonly ICloudContentService _cloudContent = Substitute.For<ICloudContentService>();
    private readonly IContentSizeMetricEmitter _metricEmitter = Substitute.For<IContentSizeMetricEmitter>();
    private readonly IOrderMetrics _metrics = Substitute.For<IOrderMetrics>();
    private readonly RetrieveOrderContentStep _step;
    private readonly StepContext _ctx = new();

    public RetrieveOrderContentStepTests()
    {
        _step = new RetrieveOrderContentStep(_cloudContent, _metricEmitter, _metrics);
    }

    private static OrderEvent CreateBaseEvent(Dictionary<string, string>? metadata = null, string? description = null)
        => new()
        {
            Type = "order-outbound",
            SubType = "general",
            Description = description ?? string.Empty,
            Metadata = metadata ?? new Dictionary<string, string>()
        };

    [Fact]
    public async Task OriginalMessagePresent_SkipsLookup()
    {
        var evt = CreateBaseEvent(new() { { "StoreId", "1" }, { "CustomerId", "1" }, { "UserId", "1" }, { "OrderReferenceId", "e" }, { "RecipientAddress", "to" }, { "SenderAddress", "from" }, { "OrderFlowType", "outbound" }, { "OrderTitle", "s" }, { "OriginalMessage", "already" } }, description: "already");
        var result = await _step.ExecuteAsync(evt, _ctx);
        Assert.True(result.ShouldContinue);
        await _cloudContent.DidNotReceive().ReadContentAsync(Arg.Any<string>());
        _metricEmitter.DidNotReceive().Emit<OrderEvent>(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task NoKey_NoLookup()
    {
        var evt = CreateBaseEvent(new() { { "StoreId", "1" }, { "CustomerId", "1" }, { "UserId", "1" }, { "OrderReferenceId", "e" }, { "RecipientAddress", "to" }, { "SenderAddress", "from" }, { "OrderFlowType", "outbound" }, { "OrderTitle", "s" } });
        var result = await _step.ExecuteAsync(evt, _ctx);
        Assert.True(result.ShouldContinue);
        await _cloudContent.DidNotReceive().ReadContentAsync(Arg.Any<string>());
        _metricEmitter.DidNotReceive().Emit<OrderEvent>(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task KeyPresent_NotFound_Continues()
    {
        var evt = CreateBaseEvent(new() { { "StoreId", "1" }, { "CustomerId", "1" }, { "UserId", "1" }, { "OrderReferenceId", "e" }, { "RecipientAddress", "to" }, { "SenderAddress", "from" }, { "OrderFlowType", "outbound" }, { "OrderTitle", "s" }, { "MessageCloudContentKey", "abc" } });
        _cloudContent.ReadContentAsync("abc", Arg.Any<CancellationToken>()).Returns((string?)null);
        var result = await _step.ExecuteAsync(evt, _ctx);
        Assert.True(result.ShouldContinue);
        Assert.Equal(string.Empty, evt.Description);
        _metricEmitter.DidNotReceive().Emit<OrderEvent>(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task KeyPresent_EmptyContent_Continues()
    {
        var evt = CreateBaseEvent(new() { { "StoreId", "1" }, { "CustomerId", "1" }, { "UserId", "1" }, { "OrderReferenceId", "e" }, { "RecipientAddress", "to" }, { "SenderAddress", "from" }, { "OrderFlowType", "outbound" }, { "OrderTitle", "s" }, { "MessageCloudContentKey", "abc" } });
        _cloudContent.ReadContentAsync("abc", Arg.Any<CancellationToken>()).Returns(string.Empty);
        var result = await _step.ExecuteAsync(evt, _ctx);
        Assert.True(result.ShouldContinue);
        Assert.Equal(string.Empty, evt.Description);
        _metricEmitter.DidNotReceive().Emit<OrderEvent>(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task KeyPresent_ContentRetrieved_SetsContext()
    {
        var ctx = new StepContext();
        var evt = CreateBaseEvent(new() { { "StoreId", "1" }, { "CustomerId", "1" }, { "UserId", "1" }, { "OrderReferenceId", "e" }, { "RecipientAddress", "to" }, { "SenderAddress", "from" }, { "OrderFlowType", "outbound" }, { "OrderTitle", "s" }, { "MessageCloudContentKey", "abc" } });
        _cloudContent.ReadContentAsync("abc", Arg.Any<CancellationToken>()).Returns("hello world");
        var result = await _step.ExecuteAsync(evt, ctx);
        Assert.True(result.ShouldContinue);
        Assert.Equal("hello world", ctx.MessageContent);
        _metricEmitter.Received(1).Emit<OrderEvent>("Custom/Order/ContentSize/Cloud/", "hello world");
    }

    [Fact]
    public async Task KeyPresent_Exception_Poisons()
    {
        var evt = CreateBaseEvent(new() { { "StoreId", "1" }, { "CustomerId", "1" }, { "UserId", "1" }, { "OrderReferenceId", "e" }, { "RecipientAddress", "to" }, { "SenderAddress", "from" }, { "OrderFlowType", "outbound" }, { "OrderTitle", "s" }, { "MessageCloudContentKey", "abc" } });
        _cloudContent.ReadContentAsync("abc", Arg.Any<CancellationToken>()).Returns<Task<string?>>(x => throw new InvalidOperationException("boom"));
        var result = await _step.ExecuteAsync(evt, _ctx);
        Assert.False(result.ShouldContinue);
        Assert.NotNull(result.Result);
        Assert.Equal(MessageResultAction.Poison, result.Result!.Action);
        _metricEmitter.DidNotReceive().Emit<OrderEvent>(Arg.Any<string>(), Arg.Any<string?>());
    }
}
