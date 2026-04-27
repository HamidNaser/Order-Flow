using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

public class PlatformEntity
{
    [BsonElement, BsonRequired]
    public required string Id { get; set; }

    [BsonElement]
    public string? OperationId { get; set; }

    public bool ShouldSerializeOperationId() => !string.IsNullOrWhiteSpace(OperationId);

    [BsonElement]
    public string? CustomerId { get; set; }

    [BsonElement]
    public string? CustomerName { get; set; }

    public bool ShouldSerializeCustomerName() => !string.IsNullOrWhiteSpace(CustomerName);

    [BsonElement]
    public string? AgentId { get; set; }

    public bool ShouldSerializeAgentId() => !string.IsNullOrWhiteSpace(AgentId);

    [BsonElement]
    public string? AgentName { get; set; }

    public bool ShouldSerializeAgentName() => !string.IsNullOrWhiteSpace(AgentName);

    [BsonElement]
    public string? TrackingId { get; set; }

    public bool ShouldSerializeTrackingId() => !string.IsNullOrWhiteSpace(TrackingId);
}
