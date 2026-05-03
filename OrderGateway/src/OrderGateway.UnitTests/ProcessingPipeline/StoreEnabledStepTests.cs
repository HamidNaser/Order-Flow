using OrderGateway.Common.FeatureToggle;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Processing.Steps;
using OrderGateway.Common.Telemetry;
using NSubstitute;
using Xunit;

namespace OrderGateway.UnitTests.ProcessingPipeline;

public class StoreEnabledStepTests
{
    private readonly IOrderMetrics _metrics = Substitute.For<IOrderMetrics>();
    private static OrderEvent CreateEvent(string storeId) => new()
    {
        Type = "order-outbound",
        SubType = "general",
        Description = "test",
        CreatedOn = DateTime.UtcNow.ToString("O"),
        Metadata = new Dictionary<string, string>
        {
            ["StoreId"] = storeId,
            ["RecipientAddress"] = "CUST-ORD-78901",
            ["SenderAddress"] = "STORE-ORD-10001",
            ["OrderFlowType"] = "outbound"
        }
    };

    [Fact]
    public async Task StoreEnabledStep_Enabled_Continues()
    {
        var featureToggle = Substitute.For<IFeatureToggle>();
        featureToggle.IsFeatureEnabled(Arg.Any<FeatureFlag>(), Arg.Any<FeatureUser?>()).Returns(true);
        var step = new StoreEnabledStep<OrderEvent>(featureToggle, _metrics);
        var evt = CreateEvent("123");
        var result = await step.ExecuteAsync(evt, new StepContext());
        Assert.True(result.ShouldContinue);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task StoreEnabledStep_Disabled_Completes()
    {
        var featureToggle = Substitute.For<IFeatureToggle>();
        featureToggle.IsFeatureEnabled(Arg.Any<FeatureFlag>(), Arg.Any<FeatureUser?>()).Returns(false);
        var step = new StoreEnabledStep<OrderEvent>(featureToggle, _metrics);
        var evt = CreateEvent("123");
        var result = await step.ExecuteAsync(evt, new StepContext());
        Assert.False(result.ShouldContinue);
        Assert.NotNull(result.Result);
        Assert.Contains("Store not enabled, skipped.", result.Result!.Details);
    }

    [Fact]
    public async Task StoreEnabledStep_PassesCorrectFeatureArgs()
    {
        var featureToggle = Substitute.For<IFeatureToggle>();
        featureToggle.IsFeatureEnabled(Arg.Any<FeatureFlag>(), Arg.Any<FeatureUser?>()).Returns(true);
        var step = new StoreEnabledStep<OrderEvent>(featureToggle, _metrics);
        var evt = CreateEvent("777");
        _ = await step.ExecuteAsync(evt, new StepContext());
        featureToggle.Received(1)
            .IsFeatureEnabled(
                FeatureFlags.OrderGatewayEnabledStoresV2,
                Arg.Is<FeatureUser>(u => u.StoreId == evt.StoreId)
            );
    }
}
