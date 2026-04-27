using OrderHub.Common.Models.Components;
using OrderHub.Contracts.Ingest;

namespace OrderHub.Common.Managers;

public interface IOrderIngestManager
{
    public Task<AddOrderResult> AddOrderAsync(OrderRequest request, Priority priority);
}

public class AddOrderResult
{
    public AddOrderResultStatus Status { get; init;  }
    public required string OrderId { get; init; }

    public static AddOrderResult NewOrder(string orderId)
    {
        return new AddOrderResult
        {
            Status = AddOrderResultStatus.NEW_ORDER,
            OrderId = orderId,
        };
    }

    public static AddOrderResult DuplicateRequest(string orderId)
    {
        return new AddOrderResult
        {
            Status = AddOrderResultStatus.DUPLICATE_REQUEST,
            OrderId = orderId,
        };
    }
}

public enum AddOrderResultStatus
{
    NEW_ORDER,
    DUPLICATE_REQUEST,
}
