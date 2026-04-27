using Order.MessageOperations.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Order.MessageOperations.Api.Services;

/// <summary>
/// Read-only service for querying the OrderHub orders collection in MongoDB/DocumentDB.
/// This service does NOT reference OrderHub.Common - it uses its own lightweight entity classes
/// to maintain full decoupling from the business layer.
/// </summary>
public class OrderQueryService
{
    private const string DatabaseName = "orders";
    private const string CollectionName = "orders";
    private const string DiscriminatorField = "_t";
    private const string StoreIdField = "StoreId";

    private readonly IMongoCollection<OrderDoc> _collection;
    private readonly ILogger<OrderQueryService> _logger;

    public OrderQueryService(IMongoClient mongoClient, ILogger<OrderQueryService> logger)
    {
        _collection = mongoClient
            .GetDatabase(DatabaseName)
            .GetCollection<OrderDoc>(CollectionName);
        _logger = logger;
    }

    /// <summary>
    /// Get a single order by StoreId and OrderId.
    /// </summary>
    public async Task<OrderRecord?> GetByIdAsync(string storeId, string orderId, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(orderId, out var objectId))
        {
            _logger.LogWarning("Invalid ObjectId format: {OrderId}", orderId);
            return null;
        }

        var filter = Builders<OrderDoc>.Filter.And(
            Builders<OrderDoc>.Filter.Eq(x => x.OrderId, objectId),
            Builders<OrderDoc>.Filter.Eq(x => x.StoreId, storeId)
        );

        var doc = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return doc != null ? MapToRecord(doc) : null;
    }

    /// <summary>
    /// List orders for a consumer within a CoOrg, sorted by date descending.
    /// </summary>
    public async Task<List<OrderRecord>> GetByCustomerAsync(
        string storeId, string customerId, int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        var filter = Builders<OrderDoc>.Filter.And(
            Builders<OrderDoc>.Filter.Eq(x => x.StoreId, storeId),
            Builders<OrderDoc>.Filter.Eq(x => x.CustomerId, customerId)
        );

        var sort = Builders<OrderDoc>.Sort.Descending(x => x.OrderDateUtc);

        var docs = await _collection.Find(filter)
            .Sort(sort)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync(ct);

        return docs.Select(MapToRecord).ToList();
    }

    /// <summary>
    /// Count orders for a consumer within a CoOrg.
    /// </summary>
    public async Task<long> CountByCustomerAsync(string storeId, string customerId, CancellationToken ct = default)
    {
        var filter = Builders<OrderDoc>.Filter.And(
            Builders<OrderDoc>.Filter.Eq(x => x.StoreId, storeId),
            Builders<OrderDoc>.Filter.Eq(x => x.CustomerId, customerId)
        );

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    /// <summary>
    /// Search orders with flexible filter criteria.
    /// </summary>
    public async Task<List<OrderRecord>> SearchAsync(
        string storeId, OrderSearchParams search, CancellationToken ct = default)
    {
        var builder = Builders<OrderDoc>.Filter;
        var filters = new List<FilterDefinition<OrderDoc>>
        {
            builder.Eq(x => x.StoreId, storeId)
        };

        if (!string.IsNullOrWhiteSpace(search.CustomerId))
            filters.Add(builder.Eq(x => x.CustomerId, search.CustomerId));

        if (!string.IsNullOrWhiteSpace(search.ChannelType))
            filters.Add(builder.Eq(DiscriminatorField, search.ChannelType.ToUpperInvariant()));

        if (!string.IsNullOrWhiteSpace(search.FulfillmentStatus))
            filters.Add(builder.Eq(x => x.FulfillmentStatus, search.FulfillmentStatus));

        if (!string.IsNullOrWhiteSpace(search.OrderFlow))
            filters.Add(builder.Eq(x => x.OrderFlow, search.OrderFlow));

        if (!string.IsNullOrWhiteSpace(search.ProviderId))
            filters.Add(builder.Eq(x => x.Provider.OrderId, search.ProviderId));

        if (!string.IsNullOrWhiteSpace(search.ProviderName))
            filters.Add(builder.Eq(x => x.Provider.Name, search.ProviderName));

        if (search.FromDate.HasValue)
            filters.Add(builder.Gte(x => x.OrderDateUtc, search.FromDate.Value));

        if (search.ToDate.HasValue)
            filters.Add(builder.Lte(x => x.OrderDateUtc, search.ToDate.Value));

        var combinedFilter = builder.And(filters);
        var sort = Builders<OrderDoc>.Sort.Descending(x => x.OrderDateUtc);

        var limit = Math.Clamp(search.Limit, 1, 200);
        var offset = Math.Max(search.Offset, 0);

        var docs = await _collection.Find(combinedFilter)
            .Sort(sort)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync(ct);

        return docs.Select(MapToRecord).ToList();
    }

    /// <summary>
    /// Get a summary of orders for a CoOrg - counts by channel type, status, direction.
    /// </summary>
    public async Task<OrderSummary> GetSummaryAsync(string storeId, CancellationToken ct = default)
    {
        var filter = Builders<OrderDoc>.Filter.Eq(x => x.StoreId, storeId);
        var totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);

        // Aggregate by channel type
        var channelTypePipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument(StoreIdField, storeId)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", $"${DiscriminatorField}" },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var channelTypeCounts = new Dictionary<string, long>();
        using (var cursor = await _collection.AggregateAsync<BsonDocument>(channelTypePipeline, cancellationToken: ct))
        {
            while (await cursor.MoveNextAsync(ct))
            {
                foreach (var doc in cursor.Current)
                {
                    var key = doc["_id"].AsString;
                    var count = doc["count"].ToInt64();
                    channelTypeCounts[key] = count;
                }
            }
        }

        // Aggregate by fulfillment status
        var statusPipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument(StoreIdField, storeId)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$FulfillmentStatus" },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var fulfillmentCounts = new Dictionary<string, long>();
        using (var cursor = await _collection.AggregateAsync<BsonDocument>(statusPipeline, cancellationToken: ct))
        {
            while (await cursor.MoveNextAsync(ct))
            {
                foreach (var doc in cursor.Current)
                {
                    var key = doc["_id"].AsString;
                    var count = doc["count"].ToInt64();
                    fulfillmentCounts[key] = count;
                }
            }
        }

        // Aggregate by direction
        var directionPipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument(StoreIdField, storeId)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$OrderFlow" },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var orderFlowCounts = new Dictionary<string, long>();
        using (var cursor = await _collection.AggregateAsync<BsonDocument>(directionPipeline, cancellationToken: ct))
        {
            while (await cursor.MoveNextAsync(ct))
            {
                foreach (var doc in cursor.Current)
                {
                    var key = doc["_id"].AsString;
                    var count = doc["count"].ToInt64();
                    orderFlowCounts[key] = count;
                }
            }
        }

        return new OrderSummary
        {
            StoreId = storeId,
            TotalCount = totalCount,
            ByChannelType = channelTypeCounts,
            ByFulfillmentStatus = fulfillmentCounts,
            ByOrderFlow = orderFlowCounts
        };
    }

    /// <summary>
    /// Find a order by provider details (provider name + provider order ID).
    /// </summary>
    public async Task<OrderRecord?> FindByProviderAsync(
        string storeId, string providerOrderId, string providerName, string? channelType = null, CancellationToken ct = default)
    {
        var builder = Builders<OrderDoc>.Filter;
        var filters = new List<FilterDefinition<OrderDoc>>
        {
            builder.Eq(x => x.StoreId, storeId),
            builder.Eq(x => x.Provider.OrderId, providerOrderId),
            builder.Eq(x => x.Provider.Name, providerName)
        };

        if (!string.IsNullOrWhiteSpace(channelType))
            filters.Add(builder.Eq(DiscriminatorField, channelType.ToUpperInvariant()));

        var filter = builder.And(filters);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return doc != null ? MapToRecord(doc) : null;
    }

    /// <summary>
    /// List recent orders for a CoOrg (regardless of consumer).
    /// </summary>
    public async Task<List<OrderRecord>> GetRecentAsync(
        string storeId, int limit = 20, CancellationToken ct = default)
    {
        var filter = Builders<OrderDoc>.Filter.Eq(x => x.StoreId, storeId);
        var sort = Builders<OrderDoc>.Sort.Descending(x => x.OrderDateUtc);

        var clampedLimit = Math.Clamp(limit, 1, 200);

        var docs = await _collection.Find(filter)
            .Sort(sort)
            .Limit(clampedLimit)
            .ToListAsync(ct);

        return docs.Select(MapToRecord).ToList();
    }

    #region Mapping

    private static OrderRecord MapToRecord(OrderDoc doc)
    {
        var record = new OrderRecord
        {
            OrderId = doc.OrderId.ToString(),
            ChannelType = doc.Discriminator ?? "UNKNOWN",
            StoreId = doc.StoreId,
            CustomerId = doc.CustomerId,
            CustomerName = doc.CustomerName,
            UserId = doc.UserId,
            UserName = doc.UserName,
            TenantId = doc.TenantId,
            ContentPreview = doc.ContentPreview,
            OrderFlow = doc.OrderFlow,
            FulfillmentStatus = doc.FulfillmentStatus,
            Priority = doc.Priority,
            OrderPlacedDateUtc = doc.OrderPlacedDateUtc,
            OrderFulfilledDateUtc = doc.OrderFulfilledDateUtc,
            OrderDateUtc = doc.OrderDateUtc,
            CreatedDate = doc.CreatedDate,
            UpdatedDate = doc.UpdatedDate,
            Provider = new ProviderInfo
            {
                Name = doc.Provider.Name,
                OrderId = doc.Provider.OrderId,
                SourceApplication = doc.Provider.SourceApplication
            }
        };

        if (doc.Solution != null)
        {
            record.Solution = new SolutionInfo
            {
                Id = doc.Solution.Id,
                OperationId = doc.Solution.OperationId,
                CustomerId = doc.Solution.CustomerId,
                CustomerName = doc.Solution.CustomerName,
                UserId = doc.Solution.UserId,
                UserName = doc.Solution.UserName,
                TrackingId = doc.Solution.TrackingId
            };
        }

        if (doc.OrderMetadata != null)
        {
            record.OrderMetadata = new OrderMetadataInfo
            {
                MediaIds = doc.OrderMetadata.MediaIds ?? [],
                ContentLength = doc.OrderMetadata.ContentLength,
                VisibleContentLength = doc.OrderMetadata.VisibleContentLength,
                PlainTextContentLength = doc.OrderMetadata.PlainTextContentLength
            };
        }

        // Channel-specific
        if (doc.OrderTitle != null) record.OrderTitle = doc.OrderTitle;
        if (doc.To != null) record.To = doc.To.Select(e => new ContactAddress { Address = e.Address, DisplayName = e.DisplayName }).ToList();
        if (doc.From != null) record.From = new ContactAddress { Address = doc.From.Address, DisplayName = doc.From.DisplayName };

        // Text-specific
        if (doc.PhoneNumbers != null) record.PhoneNumbers = new PhoneNumbers { To = doc.PhoneNumbers.To, From = doc.PhoneNumbers.From };

        // Attachments (both shipment and text can have them)
        if (doc.Attachments is { Count: > 0 })
        {
            record.Attachments = doc.Attachments.Select(a => new AttachmentInfo
            {
                AttachmentId = a.AttachmentId,
                ContentType = a.ContentType,
                Filename = a.Filename
            }).ToList();
        }

        return record;
    }

    #endregion

    #region Internal MongoDB Document Classes (read-only, decoupled from OrderHub)

    /// <summary>
    /// Lightweight BSON-mapped document class for reading the orders collection.
    /// Mirrors the OrderHub entity structure but is fully decoupled.
    /// </summary>
    [BsonIgnoreExtraElements]
    internal class OrderDoc
    {
        [BsonId]
        public ObjectId OrderId { get; set; }

        [BsonElement("_t")]
        public string? Discriminator { get; set; }

        [BsonElement]
        public double Version { get; set; }

        [BsonElement]
        public string CustomerId { get; set; } = string.Empty;

        [BsonElement]
        public string? CustomerName { get; set; }

        [BsonElement]
        public string? UserId { get; set; }

        [BsonElement]
        public string? UserName { get; set; }

        [BsonElement]
        public string StoreId { get; set; } = string.Empty;

        [BsonElement]
        public string? TenantId { get; set; }

        [BsonElement]
        public string? ContentPreview { get; set; }

        [BsonElement, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime OrderPlacedDateUtc { get; set; }

        [BsonElement, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? OrderFulfilledDateUtc { get; set; }

        [BsonElement, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime OrderDateUtc { get; set; }

        [BsonElement]
        public string OrderFlow { get; set; } = string.Empty;

        [BsonElement]
        public ProviderDoc Provider { get; set; } = new();

        [BsonElement]
        public string FulfillmentStatus { get; set; } = string.Empty;

        [BsonElement]
        public string Priority { get; set; } = string.Empty;

        [BsonElement]
        public SolutionDoc? Solution { get; set; }

        [BsonElement, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedDate { get; set; }

        [BsonElement, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedDate { get; set; }

        [BsonElement]
        public OrderMetadataDoc? OrderMetadata { get; set; }

        // Channel-specific fields
        [BsonElement, BsonIgnoreIfNull]
        public string? OrderTitle { get; set; }

        [BsonElement, BsonIgnoreIfNull]
        public List<AddressInfoDoc>? To { get; set; }

        [BsonElement, BsonIgnoreIfNull]
        public AddressInfoDoc? From { get; set; }

        // Text-specific fields
        [BsonElement, BsonIgnoreIfNull]
        public PhoneNumbersDoc? PhoneNumbers { get; set; }

        // Shared attachments
        [BsonElement, BsonIgnoreIfNull]
        public List<AttachmentDoc>? Attachments { get; set; }
    }

    [BsonIgnoreExtraElements]
    internal class ProviderDoc
    {
        [BsonElement]
        public string Name { get; set; } = string.Empty;

        [BsonElement]
        public string OrderId { get; set; } = string.Empty;

        [BsonElement]
        public string? SourceApplication { get; set; }
    }

    [BsonIgnoreExtraElements]
    internal class SolutionDoc
    {
        [BsonElement]
        public string Id { get; set; } = string.Empty;

        [BsonElement]
        public string? OperationId { get; set; }

        [BsonElement]
        public string? CustomerId { get; set; }

        [BsonElement]
        public string? CustomerName { get; set; }

        [BsonElement]
        public string? UserId { get; set; }

        [BsonElement]
        public string? UserName { get; set; }

        [BsonElement]
        public string? TrackingId { get; set; }
    }

    [BsonIgnoreExtraElements]
    internal class AddressInfoDoc
    {
        [BsonElement]
        public string Address { get; set; } = string.Empty;

        [BsonElement]
        public string? DisplayName { get; set; }
    }

    [BsonIgnoreExtraElements]
    internal class PhoneNumbersDoc
    {
        [BsonElement]
        public string To { get; set; } = string.Empty;

        [BsonElement]
        public string From { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements]
    internal class AttachmentDoc
    {
        [BsonElement]
        public string AttachmentId { get; set; } = string.Empty;

        [BsonElement]
        public string ContentType { get; set; } = string.Empty;

        [BsonElement]
        public string Filename { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements]
    internal class OrderMetadataDoc
    {
        [BsonElement]
        public List<string>? MediaIds { get; set; }

        [BsonElement]
        public int ContentLength { get; set; }

        [BsonElement]
        public int VisibleContentLength { get; set; }

        [BsonElement("ContentLengthNoHtml")]
        public int? PlainTextContentLength { get; set; }
    }

    #endregion
}
