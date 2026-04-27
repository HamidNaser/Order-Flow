using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

public class EndpointsEntity
{
    [BsonElement, BsonRequired]
    public required string To { get; set; }

    [BsonElement, BsonRequired]
    public required string From { get; set; }
}
