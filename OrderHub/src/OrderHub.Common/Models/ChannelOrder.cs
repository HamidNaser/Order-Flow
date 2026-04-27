using OrderHub.Common.Models.Components;
using Destructurama.Attributed;

namespace OrderHub.Common.Models;

public abstract class ChannelOrder
{
    public string? OrderId { get; init; }

    public required string CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? AgentId { get; set; }

    public string? AgentName { get; set; }
    public required string StoreId { get; set; }

    public string? TenantId { get; set; }

    [LogMasked]
    public string? OrderSummary { get; set; }

    public required DateTimeOffset OrderPlacedDate { get; set; }

    public DateTimeOffset? OrderFulfilledDate { get; set; }

    public required OrderFlowType OrderFlow { get; set; }

    public required Merchant Merchant { get; set; }

    public Platform? Platform { get; set; }

    public required FulfillmentStatus FulfillmentStatus { get; set; }

    public required Priority Priority { get; set; }

    public required DateTimeOffset CreatedDate { get; set; }

    public required DateTimeOffset UpdatedDate { get; set; }

    public OrderMetadata? OrderMetadata { get; set; }
}
