using OrderHub.Common.Configuration.ResourceAccess;
using OrderHub.Common.Models;
using OrderHub.Common.Models.OrderMappers;
using OrderHub.Common.Repositories.Entities;
using OrderHub.Contracts;
using OrderHub.Contracts.Common.Enums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace OrderHub.Common.Repositories;

public class OrderRepository(IMongoClient mongoClient, IOrderMapper mapper) : IOrderRepository
{
    private const string MongoDiscriminatorField = "_t";

    private readonly IMongoCollection<OrderEntity> _orderCollection = mongoClient
        .GetDatabase(MongoDbConstants.DATABASE_NAME)
        .GetCollection<OrderEntity>(MongoDbConstants.ORDER_COLLECTION);

    public async Task<long> ReadCustomerOrdersCountAsync(string storeId, string customerId)
    {
        var filterBuilder = Builders<OrderEntity>.Filter;
        var filter = filterBuilder.Eq(e => e.StoreId, storeId)
                     & filterBuilder.Eq(e => e.CustomerId, customerId);

        var count = await _orderCollection.CountDocumentsAsync(filter);

        return count;
    }


    public async Task<List<ChannelOrder>> ReadCustomerOrdersAsync(
        string storeId,
        string customerId,
        int limit,
        int offset)
    {
        var filterBuilder = Builders<OrderEntity>.Filter;
        var filter = filterBuilder.Eq(e => e.StoreId, storeId)
                     & filterBuilder.Eq(e => e.CustomerId, customerId);

        var sortDefinition = Builders<OrderEntity>.Sort
            .Descending(e => e.OrderDateUtc);

        var offsetCursor = await _orderCollection.FindAsync(filter, new FindOptions<OrderEntity>
        {
            Sort = sortDefinition,
            Limit = limit,
            Skip = offset,
        });

        var entities = await offsetCursor.ToListAsync();

        var results = (entities ?? [])
            .Select(mapper.ToInternalModel)
            .ToList();

        return results;
    }

    public async Task<ChannelOrder> InsertAsync(ChannelOrder order)
    {
        var orderEntity = mapper.ToEntity(order);
        await _orderCollection.InsertOneAsync(orderEntity);

        var result = mapper.ToInternalModel(orderEntity);
        return result;
    }

    public async Task BulkDeleteOrdersAsync(string storeId, List<string> orderIds)
    {
        var parsedIds = orderIds.Aggregate(new List<ObjectId>(), (acc, rawId) =>
        {
            if (ObjectId.TryParse(rawId, out var parsedId))
            {
                acc.Add(parsedId);
            }

            return acc;
        });
        var builder = Builders<OrderEntity>.Filter;

        var filter = builder.Eq(c => c.StoreId, storeId) & builder.In(c => c.OrderId, parsedIds);

        await _orderCollection.DeleteManyAsync(filter);
    }

    public async Task<ChannelOrder?> ReadAsync(string storeId, string orderId)
    {
        var couldParseId = ObjectId.TryParse(orderId, out var objectId);

        if (!couldParseId || string.IsNullOrWhiteSpace(storeId))
        {
            return null;
        }

        var builder = Builders<OrderEntity>.Filter;
        var filter = builder.Eq(x => x.OrderId, objectId) & builder.Eq(x => x.StoreId, storeId);

        var cursor = await _orderCollection.FindAsync<OrderEntity>(filter);
        var entity = await cursor.SingleOrDefaultAsync();

        var result = entity != null
            ? mapper.ToInternalModel(entity)
            : null;

        return result;
    }

    public async Task<long> BulkUpdateCustomerIdAsync(string storeId, IEnumerable<string> oldCustomerIds, string newCustomerId)
    {
        var oldCustomerIdsList = oldCustomerIds.ToList();

        if (oldCustomerIdsList.Count == 0)
        {
            return 0;
        }

        var filterBuilder = Builders<OrderEntity>.Filter;
        var filter = filterBuilder.Eq(e => e.StoreId, storeId)
                     & filterBuilder.In(e => e.CustomerId, oldCustomerIdsList);

        var updateBuilder = Builders<OrderEntity>.Update;
        var update = updateBuilder.Set(c => c.CustomerId, newCustomerId);

        var result = await _orderCollection.UpdateManyAsync(filter, update);

        return result.ModifiedCount;
    }

    public async Task<ChannelOrder?> FindByMerchantDetailsAsync(
        string storeId,
        string merchantOrderId,
        MerchantName merchantName,
        ChannelType channelType
    )
    {
        var builder = Builders<OrderEntity>.Filter;
        var channelTypeDiscriminator = GetChannelTypeDiscriminator(channelType);

        var filter = builder.And(
            builder.Eq(x => x.StoreId, storeId),
            builder.Eq(x => x.Merchant.OrderId, merchantOrderId),
            builder.Eq(x => x.Merchant.Name, merchantName.ToString()),
            builder.Eq(MongoDiscriminatorField, channelTypeDiscriminator)
        );

        var cursor = await _orderCollection.FindAsync(filter);
        var entity = await cursor.SingleOrDefaultAsync();

        var result = entity != null
            ? mapper.ToInternalModel(entity)
            : null;

        return result;
    }

    public async Task<ChannelOrder?> FindAndUpdateFulfillmentStatusAsync(
        string storeId,
        string merchantOrderId,
        MerchantName merchantName,
        ChannelType channelType,
        Models.Components.FulfillmentStatus newStatus,
        DateTimeOffset statusUpdatedDate
    )
    {
        var builder = Builders<OrderEntity>.Filter;
        var channelTypeDiscriminator = GetChannelTypeDiscriminator(channelType);

        var filter = builder
            .And(
                builder.Eq(x => x.StoreId, storeId),
                builder.Eq(x => x.Merchant.OrderId, merchantOrderId),
                builder.Eq(x => x.Merchant.Name, merchantName.ToString()),
                builder.Eq(MongoDiscriminatorField, channelTypeDiscriminator),
                builder.Eq(
                    x => x.FulfillmentStatus,
                    nameof(Models.Components.FulfillmentStatus.IN_PROGRESS)
                )
            );

        var update = Builders<OrderEntity>.Update
            .Set(x => x.FulfillmentStatus, newStatus.ToString())
            .Set(x => x.UpdatedDate, DateTimeOffset.UtcNow.UtcDateTime);

        if (newStatus == Models.Components.FulfillmentStatus.SUCCESS)
        {
            update = update.Set(
                x => x.OrderFulfilledDateUtc,
                statusUpdatedDate.UtcDateTime
            );
        }

        var options = new FindOneAndUpdateOptions<OrderEntity>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updatedEntity = await _orderCollection.FindOneAndUpdateAsync(filter, update, options);

        return updatedEntity != null ? mapper.ToInternalModel(updatedEntity) : null;
    }

    private static string GetChannelTypeDiscriminator(ChannelType channelType)
    {
        return channelType switch
        {
            ChannelType.STANDARD => ChannelTypeConstants.StandardDiscriminatorValue,
            ChannelType.DIGITAL => ChannelTypeConstants.DigitalDiscriminatorValue,
            _ => throw new ArgumentException($"Unknown channel type: {channelType}", nameof(channelType))
        };
    }
}
