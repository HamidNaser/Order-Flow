using Order.MessagePump.Messages;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Models;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Services;

namespace OrderGateway.Common.Processing.Steps;

// Final step to send orders to ingest APIs.
public sealed class SendOrderStep<TEvent>(IOrderService orderService) : IProcessingStep<TEvent> where TEvent : IOrderEvent
{
    public async Task<StepResult> ExecuteAsync(TEvent evt, StepContext context, CancellationToken ct = default)
    {
        var eventName = evt!.GetType().Name;
        var eventNameWithoutEvent = eventName.Replace("Event", "");

        var ingestResult = await orderService.SendAsync(evt, context, ct);

        if (ingestResult.Status == OrderIngestStatus.OrderIngested)
        {
            context.OrderId = ingestResult.OrderId;
            return StepResult.Continue();
        }

        if (ingestResult is { Status: OrderIngestStatus.OrderDuplicate, Reason: not null })
        {
            return StepResult.Complete(ingestResult.Reason);
        }

        NewRelic.Api.Agent.NewRelic.IncrementCounter($"Custom/{eventNameWithoutEvent}/Processing/Error/OrderApiIngestFailed");
        return StepResult.Complete(MessageResult.Poison(reason: ingestResult.Reason));
    }
}
