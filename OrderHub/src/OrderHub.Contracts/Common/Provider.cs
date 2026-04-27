using System.ComponentModel.DataAnnotations;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Contracts.Common;

/// <summary>
/// Contains information about the merchant and source of the order.
/// </summary>
public class Merchant
{
    [Required]
    public required MerchantName Name { get; set; }

    /// <summary>
    /// The unique identifier for this order in the merchant's system.
    /// </summary>
    [StringLength(255, MinimumLength = 1)]
    [Required]
    public required string OrderId { get; set; }

    /// <summary>
    /// Name of the source application that initiated this order request from the Merchant.
    /// </summary>
    /// <example>Mobile App, Desktop App</example>
    public string? SourceApplication { get; set; }
}
