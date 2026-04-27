using OrderHub.Common.Models;
using OrderHub.Common.Services;

namespace OrderHub.Common.Managers;

public interface IOrderManager
{
    public Task<(long ordersCount, List<ChannelOrder> results)> ReadCustomerOrdersAsync(
        string storeId,
        string customerId,
        int page = 1,
        int pageSize = 25
    );

    public Task BulkDeleteOrdersAsync(string storeId, List<string> orderIds);

    public Task<(ChannelOrder? Order, string? Content)> GetFullOrderByIdAsync(
        string storeId,
        string orderId
    );

    /// <summary>
    /// Retrieves order content by encoded S3 key.
    /// Returns (content, isNotFound) tuple where:
    /// - content: The retrieved content string if successful, null otherwise
    /// - isNotFound: True if S3 object not found (404), false for other errors (400)
    /// </summary>
    public Task<string?> GetOrderContentByEncodedKeyAsync(string encodedKey);
}
