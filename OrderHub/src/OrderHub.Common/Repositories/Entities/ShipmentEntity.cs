using OrderHub.Contracts;
using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

[BsonDiscriminator(ChannelTypeConstants.StandardDiscriminatorValue, Required = true)]
public class ShipmentEntity : OrderEntity
{
    [BsonElement]
    public string? OrderTitle { get; set; }

    public bool ShouldSerializeOrderTitle() => !string.IsNullOrWhiteSpace(OrderTitle);

    [BsonElement, BsonRequired]
    public required List<AddressInfoEntity> To { get; set; }

    [BsonElement, BsonRequired]
    public required AddressInfoEntity From { get; set; }
}
