using System.ComponentModel.DataAnnotations;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Contracts.Common;

/// <summary>
/// Provides business context and platform (Business Unit) information related to a order.
/// </summary>
public class Platform
{
    [Required]
    public required PlatformId Id { get; set; }

    /// <summary>
    /// Business Operation Identifier - the platform-specific unique instance identifier for the platform (store identifier).
    /// </summary>
    /// <example>BO_SALES_456789</example>
    public string? OperationId { get; set; }

    /// <summary>
    /// The platform-specific unique identifier for the customer.
    /// </summary>
    /// <example>CUST_789012</example>
    public string? CustomerId { get; set; }

    /// <summary>
    /// The platform-specific full name for the customer.
    /// </summary>
    /// <example>John Smith</example>
    public string? CustomerName { get; set; }

    /// <summary>
    /// The platform-specific unique identifier for the user.
    /// </summary>
    /// <example>SALES_REP_345</example>
    public string? AgentId { get; set; }

    /// <summary>
    /// The platform-specific full name for the user.
    /// </summary>
    /// <example>Jane Smith</example>
    public string? AgentName { get; set; }

    /// <summary>
    /// The platform-specific unique identifier for tracking an order through fulfillment.
    /// </summary>
    /// <example>TRACK_001, REPAIR_ORDER_002</example>
    public string? TrackingId { get; set; }
}
