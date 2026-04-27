using OrderGateway.Common.Models;
using OrderGateway.Common.Models.Events;

namespace OrderGateway.Common.Managers
{
    public interface IOrderEventManager
    {
        Task<ProcessingResult> ProcessEvent(OrderEvent orderEvent);
    }
}
