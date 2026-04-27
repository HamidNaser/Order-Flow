using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Processing;
using OrderGateway.Common.Processing.Steps;
using Order.MessagePump.Messages;
using Xunit;

namespace OrderGateway.UnitTests.ProcessingPipeline;

public class ActionStepTests
{
    private static OrderEvent CreateEvent() => new()
    {
        Type = "order-outbound",
        SubType = "general",
        Description = "test",
        CreatedOn = DateTime.UtcNow.ToString("O"),
        Metadata = new Dictionary<string, string>
        {
            ["StoreId"] = "1",
            ["RecipientAddress"] = "CUST-ORD-78901",
            ["SenderAddress"] = "STORE-ORD-10001",
            ["OrderFlowType"] = "outbound"
        }
    };

    [Fact]
    public async Task ActionStep_ExecutesActionAndContinues()
    {
        bool executed = false;
        var step = new ActionStep<OrderEvent>((evt, ctx, ct) =>
        {
            executed = true;
            ctx.MessageContent = "test content";
            return Task.CompletedTask;
        });
        var result = await step.ExecuteAsync(CreateEvent(), new StepContext());
        Assert.True(executed);
        Assert.True(result.ShouldContinue);
    }

    [Fact]
    public async Task ActionStep_WhenActionThrows_ExceptionBubbles()
    {
        var step = new ActionStep<OrderEvent>((evt, ctx, ct) => throw new InvalidOperationException("boom"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteAsync(CreateEvent(), new StepContext()));
    }

    [Fact]
    public async Task ActionStep_CanReturnCustomStepResult()
    {
        var step = new ActionStep<OrderEvent>((evt, ctx, ct) => Task.FromResult(StepResult.Complete(MessageResult.Retry(details: "custom"))));
        var result = await step.ExecuteAsync(CreateEvent(), new StepContext());
        Assert.False(result.ShouldContinue);
        Assert.NotNull(result.Result);
        Assert.Contains("custom", result.Result!.Details);
    }
}
