using System.ComponentModel.DataAnnotations;
using OrderHub.Contracts.Common;

namespace OrderHub.Contracts.Access;

/// <summary>
/// The shipment order response model.
/// <remarks>
/// <para>This model has shipment-specific order properties.</para>
/// <para>A discriminator field <c>ChannelType</c> on GetOrderResponse correlates the polymorphic inheritance.</para>
/// </remarks>
/// </summary>
public class GetShipmentResponse : GetOrderResponse
{
    /// <summary>
    /// The list of <c>To</c> recipients for the shipment order.
    /// </summary>
    [Required]
    public required List<AddressInfo> To { get; set; }

    /// <summary>
    /// The comma-delimited string of ALL <c>To</c> recipients for the shipment order.
    /// <remarks>
    /// <para>If a recipient has a Name, the entry will include the display name alongside the address. Otherwise, formatted as address only.</para>
    /// </remarks>
    /// </summary>
    /// <example>"John Doe" &lt;ORD-ADDR-001&gt;, ORD-ADDR-002, "Bob Johnson" &lt;ORD-ADDR-003&gt;</example>
    [Required]
    public required string FormattedToRecipients { get; set; }

    /// <summary>
    /// The <c>From</c> sender details for the shipment order.
    /// </summary>
    [Required]
    public required AddressInfo From { get; set; }

    /// <summary>
    /// The title or heading for the shipment order.
    /// <remarks>
    /// <para>The order body is set using the Content field.</para>
    /// </remarks>
    /// </summary>
    /// <example>Order #4521 - Vehicle Purchase</example>
    public string? OrderTitle { get; set; }
}
