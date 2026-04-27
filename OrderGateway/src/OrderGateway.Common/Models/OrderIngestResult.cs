namespace OrderGateway.Common.Models;

public enum OrderIngestStatus
{
    OrderIngested,
    OrderInvalid,
    OrderDuplicate
}

public sealed record OrderIngestResult(
    OrderIngestStatus Status,
    string? OrderId = null,
    string? Reason = null
)
{
    public static OrderIngestResult Ingested(string? id)
        => new(OrderIngestStatus.OrderIngested, id, null);

    public static OrderIngestResult Invalid(string? reason)
        => new(OrderIngestStatus.OrderInvalid, null, reason);

    public static OrderIngestResult Duplicate(string? reason = null)
        => new(OrderIngestStatus.OrderDuplicate, null, reason);
}
