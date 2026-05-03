using Order.MessageOperations.Api.Models;

namespace Order.MessageOperations.Api.Services;

public interface IOrderQueryService
{
    Task<OrderRecord?> GetByIdAsync(string storeId, string orderId, CancellationToken ct = default);

    Task<List<OrderRecord>> GetByCustomerAsync(
        string storeId, string customerId, int limit = 50, int offset = 0, CancellationToken ct = default);

    Task<long> CountByCustomerAsync(string storeId, string customerId, CancellationToken ct = default);

    Task<List<OrderRecord>> SearchAsync(string storeId, OrderSearchParams search, CancellationToken ct = default);

    Task<OrderSummary> GetSummaryAsync(string storeId, CancellationToken ct = default);

    Task<OrderRecord?> FindByProviderAsync(
        string storeId, string providerOrderId, string providerName, string? channelType = null, CancellationToken ct = default);

    Task<List<OrderRecord>> GetRecentAsync(string storeId, int limit = 20, CancellationToken ct = default);
}
