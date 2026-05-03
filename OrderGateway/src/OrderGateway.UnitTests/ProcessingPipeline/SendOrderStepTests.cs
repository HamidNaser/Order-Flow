using Order.MessagePump.Messages;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Models;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Processing.Steps;
using OrderGateway.Common.Services;
using OrderGateway.Common.Telemetry;
using NSubstitute;
using Xunit;

namespace OrderGateway.UnitTests.ProcessingPipeline;

public class SendOrderStepTests
{
    private readonly IOrderService commsSvc = Substitute.For<IOrderService>();
    private readonly IOrderMetrics metrics = Substitute.For<IOrderMetrics>();

    [Fact]
    public async Task ExecuteAsync_Success_Continues()
    {
        var step = new SendOrderStep<OrderEvent>(commsSvc, metrics);
        var evt = BuildOrder();
        var context = new StepContext();
        commsSvc.SendAsync(evt, Arg.Any<StepContext>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(OrderIngestResult.Ingested("abc")));
        var result = await step.ExecuteAsync(evt, context);
        Assert.True(result.ShouldContinue);
        Assert.Null(result.Result); // Continue has no MessageResult
        Assert.Equal("abc", context.OrderId);
        await commsSvc.Received(1).SendAsync(evt, Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Invalid_Poisons()
    {
        var step = new SendOrderStep<OrderEvent>(commsSvc, metrics);
        var evt = BuildOrder();
        commsSvc.SendAsync(evt, Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Invalid("validation failed")));

        var context = new StepContext();
        var result = await step.ExecuteAsync(evt, context);

        Assert.False(result.ShouldContinue);
        Assert.NotNull(result.Result);
        Assert.Equal(MessageResultAction.Poison, result.Result!.Action);
    }

    [Fact]
    public async Task ExecuteAsync_Duplicate_CompletesWithDetails()
    {
        var step = new SendOrderStep<OrderEvent>(commsSvc, metrics);
        var evt = BuildOrder();
        commsSvc.SendAsync(evt, Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Duplicate("Duplicate order: existing-id")));

        var context = new StepContext();
        var result = await step.ExecuteAsync(evt, context);

        Assert.False(result.ShouldContinue);
        Assert.NotNull(result.Result);
        Assert.Equal(MessageResultAction.Complete, result.Result!.Action);
        Assert.Equal("Duplicate order: existing-id", result.Result!.Details);
    }

    private static OrderEvent BuildOrder() => new()
    {
        Description = "body",
        CreatedOn = DateTime.UtcNow.ToString(),
        Metadata = new Dictionary<string, string>
        {
            {"StoreId", "1"},
            {"CustomerId", "2"},
            {"RecipientAddress", "CUST-ORD-78901"},
            {"SenderAddress", "STORE-ORD-10001"},
            {"OrderTitle", "Test Order Title"},
            {"OrderFlowType", "outbound"},
            {"OrderReferenceId", Guid.NewGuid().ToString()},
            {"UserId", "123"}
        }
    };
}
