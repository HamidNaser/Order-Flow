using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Exceptions;
using OrderHub.Common.Models;
using OrderHub.Common.Repositories;
using OrderHub.Common.Services;
using OrderHub.Common.Utilities;
using OrderHub.Contracts.Common.Enums;
using OrderHub.Contracts.Ingest;
using OrderHub.Contracts.Utility;
using Serilog;

namespace OrderHub.Common.Managers;

public class OrderManager(
    IOrderRepository repository,
    IS3Service s3Service,
    S3Config s3Config) : IOrderManager
{
    public async Task<(long ordersCount, List<ChannelOrder> results)> ReadCustomerOrdersAsync(
        string storeId,
        string customerId,
        int page = 1,
        int pageSize = 25
    )
    {
        var offset = pageSize * (page - 1);

        var countTask = repository.ReadCustomerOrdersCountAsync(storeId, customerId);
        var ordersTask = repository.ReadCustomerOrdersAsync(storeId, customerId, pageSize, offset);

        await Task.WhenAll(countTask, ordersTask);

        return (countTask.Result, ordersTask.Result);
    }

    public async Task BulkDeleteOrdersAsync(string storeId, List<string> orderIds)
    {
        await repository.BulkDeleteOrdersAsync(storeId, orderIds);
    }

    public async Task<(ChannelOrder? Order, string? Content)> GetFullOrderByIdAsync(
        string storeId,
        string orderId
    )
    {
        var order = await repository.ReadAsync(storeId, orderId);
        if (order == null)
        {
            return (null, null);
        }

        var channelType = GetChannelType(order);

        var s3Key = new S3OrderKey
        {
            Priority = order.Priority,
            MerchantName = order.Merchant.Name,
            ChannelType = channelType,
            SourceOrderId = order.Merchant.OrderId,
            OrderId = order.OrderId!
        };

        string? s3Content = null;
        var s3Response = await s3Service.GetObjectAsync<OrderRequest>(s3Config.OrderBucketName, s3Key.ToKeyString());
        if (s3Response.ErrorType == S3ErrorType.NONE)
        {
            s3Content = s3Response.Content?.Content;
        }
        else
        {
            Log.Warning("Failed to retrieve S3 content for order {OrderId} with key {S3Key} : {ErrorType} - {ErrorMessage}",
                orderId, s3Key.ToKeyString(), s3Response.ErrorType, s3Response.ErrorMessage);
        }
        return (order, s3Content);
    }

    public async Task<string?> GetOrderContentByEncodedKeyAsync(string encodedKey)
    {
        string? decodedKey;

        decodedKey = Base64UrlTextEncoderHelper.Decode(encodedKey);

        if (string.IsNullOrEmpty(decodedKey))
        {
            Log.Warning("Failed to decode S3 order key. EncodedKey: {EncodedKey}", encodedKey);
            return null;
        }

        var s3Response = await s3Service.GetObjectAsync<OrderRequest>(s3Config.OrderBucketName, decodedKey);

        // Return early on success
        if (s3Response.ErrorType == S3ErrorType.NONE)
        {
            return s3Response.Content?.Content;
        }

        // Handle S3 NOT_FOUND vs other errors
        if (s3Response.ErrorType == S3ErrorType.NOT_FOUND)
        {
            Log.Warning("S3 object not found. DecodedKey: {DecodedKey}", decodedKey);
            return null;
        }

        Log.Warning("Error retrieving S3 object. DecodedKey: {DecodedKey}, ErrorType: {ErrorType}, ErrorMessage: {ErrorMessage}",
            decodedKey, s3Response.ErrorType, s3Response.ErrorMessage);
        return null;
    }

    private static ChannelType GetChannelType(ChannelOrder order)
    {
        return order switch
        {
            ShipmentOrder => ChannelType.STANDARD,
            DigitalOrder => ChannelType.DIGITAL,
            _ => throw new UnregisteredChannelTypeException(order.GetType().Name)
        };
    }
}
