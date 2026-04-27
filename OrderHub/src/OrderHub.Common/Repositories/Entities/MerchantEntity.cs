using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

public class MerchantEntity
{
    [BsonElement, BsonRequired]
    public required string Name { get; set; }

    [BsonElement, BsonRequired]
    public required string OrderId { get; set; }

    [BsonElement]
    public string? SourceApplication { get; set; }

    public bool ShouldSerializeSourceApplication() => !string.IsNullOrWhiteSpace(SourceApplication);
}
