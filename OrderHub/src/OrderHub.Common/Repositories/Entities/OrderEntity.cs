using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

[BsonKnownTypes(typeof(ShipmentEntity), typeof(DigitalEntity))]
public abstract class OrderEntity
{
    [BsonId]
    public required ObjectId OrderId { get; init; }

    [BsonElement, BsonRequired]
    public double Version { get; set; } = 1.0;

    [BsonElement, BsonRequired]
    public required string CustomerId { get; set; }

    [BsonElement]
    public string? CustomerName { get; set; }

    public bool ShouldSerializeCustomerName() => !string.IsNullOrWhiteSpace(CustomerName);

    [BsonElement]
    public string? AgentId { get; set; }

    public bool ShouldSerializeAgentId() => !string.IsNullOrWhiteSpace(AgentId);

    [BsonElement]
    public string? AgentName { get; set; }

    public bool ShouldSerializeAgentName() => !string.IsNullOrWhiteSpace(AgentName);

    [BsonElement, BsonRequired]
    public required string StoreId { get; set; }

    [BsonElement]
    public string? TenantId { get; set; }

    public bool ShouldSerializeTenantId() => !string.IsNullOrWhiteSpace(TenantId);

    [BsonElement]
    public string? OrderSummary { get; set; }

    public bool ShouldSerializeOrderSummary() => !string.IsNullOrWhiteSpace(OrderSummary);

    [BsonElement, BsonRequired, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime OrderPlacedDateUtc { get; set; }

    [BsonElement, BsonIgnoreIfNull, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? OrderFulfilledDateUtc { get; set; }

    [BsonElement, BsonRequired, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime OrderDateUtc { get; set; }

    [BsonElement, BsonRequired]
    public required string OrderFlow { get; set; }

    [BsonElement, BsonRequired]
    public required MerchantEntity Merchant { get; set; }

    [BsonElement, BsonRequired]
    public required string FulfillmentStatus { get; set; }

    [BsonElement, BsonRequired]
    public required string Priority { get; set; }

    [BsonElement, BsonIgnoreIfNull]
    public PlatformEntity? Platform { get; set; }

    [BsonElement, BsonRequired, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime CreatedDate { get; set; }

    [BsonElement, BsonRequired, BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UpdatedDate { get; set; }

    [BsonElement]
    public OrderMetadataEntity? OrderMetadata { get; set; }

    /// <summary>
    /// Determines whether OrderMetadata should be serialized to MongoDB.
    /// </summary>
    /// <returns>True if OrderMetadata is not null and has at least one meaningful property (MediaIds with count > 0, or any non-null length property).</returns>
    public bool ShouldSerializeOrderMetadata() =>
        OrderMetadata != null &&
        (OrderMetadata.ShouldSerializeMediaIds() ||
         OrderMetadata.ContentLength != default ||
         OrderMetadata.VisibleContentLength != default ||
         OrderMetadata.ShouldSerializePlainTextContentLength());
}
