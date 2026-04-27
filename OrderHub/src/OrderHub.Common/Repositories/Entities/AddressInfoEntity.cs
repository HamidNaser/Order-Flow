using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

public class AddressInfoEntity
{
    [BsonElement, BsonRequired]
    public required string Address { get; set; }

    [BsonElement]
    public string? Name { get; set; }

    public bool ShouldSerializeName() => !string.IsNullOrEmpty(Name);
}
