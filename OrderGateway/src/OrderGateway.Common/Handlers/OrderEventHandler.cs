using Amazon.SQS.Model;
using OrderGateway.Common.Configuration;
using OrderGateway.Common.Managers;
using OrderGateway.Common.Models;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Telemetry;
using Serilog.Context;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderGateway.Common.Configuration.Handlers;

namespace OrderGateway.Common.Handlers;

public class OrderEventHandler(IOrderEventManager orderEventManager, IOrderMetrics metrics, IOptions<MessageHandlerOptions> options) : BaseEventHandler<OrderEvent>(metrics, options)
{
    protected override string EventType => "Order";

    protected internal override OrderEvent ParseEvent(Message message)
    {
        if (message == null) throw new InvalidOperationException("Order event message is null");
        if (string.IsNullOrWhiteSpace(message.Body)) throw new InvalidOperationException("Order event message body is null");

        var decodedBody = Convert.FromBase64String(message.Body);
        var orderEvent = JsonSerializer.Deserialize<OrderEvent>(decodedBody, SerializationConfig.DefaultSettings)
                        ?? throw new InvalidOperationException("Failed to deserialize OrderEvent");

        return orderEvent;
    }

    protected override async Task<ProcessingResult> ProcessEvent(OrderEvent evt, CancellationToken cancellationToken = default)
        => await orderEventManager.ProcessEvent(evt, cancellationToken);

    protected override DisposableList CreateLogContext(OrderEvent orderEvent)
    {
        var disposables = new DisposableList
        {
            LogContext.PushProperty(nameof(OrderEvent), orderEvent, destructureObjects: true),
            LogContext.PushProperty("ApproximateReceiveCount", orderEvent.ApproximateReceiveCount)
        };

        if (orderEvent.Metadata != null)
        {
            AddMetadataLogContext("StoreId");
            AddMetadataLogContext("UserId");
            AddMetadataLogContext("CustomerId");
            AddMetadataLogContext("TrackingRef");
            AddMetadataLogContext("SourceTrackingId");
            AddMetadataLogContext("Classification");
            AddMetadataLogContext("OrderReferenceId");
            AddMetadataLogContext("MessageId");
            AddMetadataLogContext("OrderTitle", logMasked: true);
            AddMetadataLogContext("SenderAddress");
            AddMetadataLogContext("RecipientAddress");
            AddMetadataLogContext("OrderFlowType");
            AddMetadataLogContext("HasAttachments");
            AddMetadataLogContext("OrderFlags");
            AddMetadataLogContext("OrderTypeId");
        }

        return disposables;

        void AddMetadataLogContext(string field, bool logMasked = false)
        {
            var value = orderEvent.GetMetadataValue(field);
            if (!string.IsNullOrWhiteSpace(value))
            {
                disposables.Add(LogContext.PushProperty($"Metadata.{field}", logMasked ? "***" : value));
            }
        }
    }
}
