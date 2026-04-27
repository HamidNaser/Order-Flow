using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Models;

namespace OrderGateway.Common.Services;

public interface IOrderService
{
    Task<OrderIngestResult> SendAsync(IOrderEvent evt, Processing.Abstractions.StepContext context, CancellationToken ct = default);
}
