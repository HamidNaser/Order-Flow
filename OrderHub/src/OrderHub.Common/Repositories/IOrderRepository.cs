using OrderHub.Common.Models;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Common.Repositories;

public interface IOrderRepository
{
    public Task<ChannelOrder?> ReadAsync(string storeId, string orderId);
    public Task<ChannelOrder> InsertAsync(ChannelOrder order);
    public Task<long> ReadCustomerOrdersCountAsync(string storeId, string customerId);

    public Task<List<ChannelOrder>> ReadCustomerOrdersAsync(string storeId, string customerId, int limit = 100, int offset = 0);
    public Task BulkDeleteOrdersAsync(string storeId, List<string> orderIds);

    public Task<long> BulkUpdateCustomerIdAsync(string storeId, IEnumerable<string> oldCustomerIds, string newCustomerId);

    public Task<ChannelOrder?> FindByMerchantDetailsAsync(
        string storeId,
        string merchantOrderId,
        MerchantName merchantName,
        ChannelType channelType);

    public Task<ChannelOrder?> FindAndUpdateFulfillmentStatusAsync(
        string storeId,
        string merchantOrderId,
        MerchantName merchantName,
        ChannelType channelType,
        Models.Components.FulfillmentStatus newStatus,
        DateTimeOffset statusUpdatedDate);
}
