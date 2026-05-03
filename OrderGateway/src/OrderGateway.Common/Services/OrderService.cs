using OrderGateway.Common.Clients.IngestStandardApi.V1;
using OrderGateway.Common.Clients.IngestExpressApi.V1;
using OrderGateway.Common.Models;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Services.Mapping;
using OrderGateway.Common.Telemetry;
using Serilog;
using StandardContracts = OrderGateway.Common.Clients.IngestStandardApi.V1;
using ExpressContracts = OrderGateway.Common.Clients.IngestExpressApi.V1;

namespace OrderGateway.Common.Services;

public class OrderService(
    IIngestStandardClient standardClient,
    IIngestExpressClient expressClient,
    IOrderRequestMapper orderMapper,
    IOrderMetrics metrics,
    ILogger logger
) : IOrderService
{
    public async Task<OrderIngestResult> SendAsync(IOrderEvent evt, StepContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        try
        {
            return evt switch
            {
                OrderEvent order => await SendOrderAsync(order, context, evt.IsStandardPriority, ct),
                _ => OrderIngestResult.Invalid("Unsupported event type")
            };
        }
        catch (StandardContracts.OrderGatewayApiV1ClientException<StandardContracts.HttpValidationProblemDetails> standardKnown) when (standardKnown.StatusCode == 400)
        {
            logger
                .ForContext("HttpValidationProblemDetails", standardKnown.Result, destructureObjects: true)
                .Error(standardKnown, "{Service}: Validation failure (standard) status=400", nameof(OrderService));

            return OrderIngestResult.Invalid($"Validation failed: {standardKnown.Message}");
        }
        catch (ExpressContracts.OrderGatewayApiV1ClientException<ExpressContracts.HttpValidationProblemDetails> expressKnown) when (expressKnown.StatusCode == 400)
        {
            logger
                .ForContext("HttpValidationProblemDetails", expressKnown.Result, destructureObjects: true)
                .Error(expressKnown, "{Service}: Validation failure (express) status=400", nameof(OrderService));

            return OrderIngestResult.Invalid($"Validation failed: {expressKnown.Message}");
        }
        catch (StandardContracts.OrderGatewayApiV1ClientException<StandardContracts.DuplicateOrderResponse> standardDuplicate) when (standardDuplicate.StatusCode == 409)
        {
            var eventNameWithoutEvent = evt.GetType().Name.Replace("Event", "");
            metrics.IncrementCounter($"Custom/{eventNameWithoutEvent}/Processing/Error/DuplicateOrder");

            logger
                .ForContext("DuplicateOrderResponse", standardDuplicate.Result, destructureObjects: true)
                .Debug("{Service}: Duplicate order (standard) status=409", nameof(OrderService));

            return OrderIngestResult.Duplicate($"Duplicate order: {standardDuplicate.Message}");
        }
        catch (ExpressContracts.OrderGatewayApiV1ClientException<ExpressContracts.DuplicateOrderResponse> expressDuplicate) when (expressDuplicate.StatusCode == 409)
        {
            var eventNameWithoutEvent = evt.GetType().Name.Replace("Event", "");
            metrics.IncrementCounter($"Custom/{eventNameWithoutEvent}/Processing/Error/DuplicateOrder");

            logger
                .ForContext("DuplicateOrderResponse", expressDuplicate.Result, destructureObjects: true)
                .Debug("{Service}: Duplicate order (express) status=409", nameof(OrderService));

            return OrderIngestResult.Duplicate($"Duplicate order: {expressDuplicate.Message}");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Service}: Unexpected exception sending order type {EventType}", nameof(OrderService), evt.GetType().Name);
            throw;
        }
    }

    private async Task<OrderIngestResult> SendOrderAsync(OrderEvent order, StepContext context, bool isStandard, CancellationToken ct)
    {
        if (isStandard)
        {
            var standardResponse = await standardClient
                .WithCorrelationId(order.CorrelationId)
                .AddShipmentOrderAsync(orderMapper.MapStandard(order, context), ct);

            return OrderIngestResult.Ingested(standardResponse.Id);
        }

        var expressResponse = await expressClient
            .WithCorrelationId(order.CorrelationId)
            .AddShipmentOrderAsync(orderMapper.MapExpress(order, context), ct);

        return OrderIngestResult.Ingested(expressResponse.Id);
    }

}
