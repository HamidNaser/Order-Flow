namespace Order.MessageOperations.Api.Models;

/// <summary>
/// Read-only DTO representing an order record from the database.
/// Decoupled from OrderHub entities - this is a flattened view for operational tooling.
/// </summary>
public class OrderRecord
{
    public string OrderId { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? TenantId { get; set; }
    public string? ContentPreview { get; set; }
    public string OrderFlow { get; set; } = string.Empty;
    public string FulfillmentStatus { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime OrderPlacedDateUtc { get; set; }
    public DateTime? OrderFulfilledDateUtc { get; set; }
    public DateTime OrderDateUtc { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public ProviderInfo Provider { get; set; } = new();
    public SolutionInfo? Solution { get; set; }
    public OrderMetadataInfo? OrderMetadata { get; set; }

    // Channel-specific fields
    public string? OrderTitle { get; set; }
    public List<ContactAddress>? To { get; set; }
    public ContactAddress? From { get; set; }

    // Text-specific fields
    public PhoneNumbers? PhoneNumbers { get; set; }

    // Shared
    public List<AttachmentInfo>? Attachments { get; set; }
}

public class ProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string? SourceApplication { get; set; }
}

public class SolutionInfo
{
    public string Id { get; set; } = string.Empty;
    public string? OperationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? TrackingId { get; set; }
}

public class ContactAddress
{
    public string Address { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public class PhoneNumbers
{
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}

public class AttachmentInfo
{
    public string AttachmentId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
}

public class OrderMetadataInfo
{
    public List<string> MediaIds { get; set; } = [];
    public int ContentLength { get; set; }
    public int VisibleContentLength { get; set; }
    public int? PlainTextContentLength { get; set; }
}

/// <summary>
/// Summary of orders count by status or channel type.
/// </summary>
public class OrderSummary
{
    public string StoreId { get; set; } = string.Empty;
    public long TotalCount { get; set; }
    public Dictionary<string, long> ByChannelType { get; set; } = new();
    public Dictionary<string, long> ByFulfillmentStatus { get; set; } = new();
    public Dictionary<string, long> ByOrderFlow { get; set; } = new();
}

/// <summary>
/// Search/filter parameters for orders.
/// </summary>
public class OrderSearchParams
{
    public string? CustomerId { get; set; }
    public string? ChannelType { get; set; }
    public string? FulfillmentStatus { get; set; }
    public string? OrderFlow { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}
