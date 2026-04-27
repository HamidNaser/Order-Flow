using OrderHub.Contracts;
using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

[BsonDiscriminator(ChannelTypeConstants.DigitalDiscriminatorValue, Required = true)]
public class DigitalEntity : OrderEntity
{
    [BsonElement, BsonRequired]
    public required EndpointsEntity Endpoints { get; set; }
}
