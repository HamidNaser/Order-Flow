using StandardContracts = OrderGateway.Common.Clients.IngestStandardApi.V1;
using ExpressContracts = OrderGateway.Common.Clients.IngestExpressApi.V1;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Processing.Abstractions;

namespace OrderGateway.Common.Services.Mapping;

public interface IOrderRequestMapper
{
    StandardContracts.AddShipmentOrderRequest MapStandard(OrderEvent orderEvent, StepContext context);
    ExpressContracts.AddShipmentOrderRequest MapExpress(OrderEvent orderEvent, StepContext context);
}
