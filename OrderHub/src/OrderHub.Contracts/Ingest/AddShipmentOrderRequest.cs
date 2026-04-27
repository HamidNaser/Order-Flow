using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OrderHub.Contracts.Common;
using OrderHub.Contracts.Common.Enums;
using Destructurama.Attributed;

namespace OrderHub.Contracts.Ingest;

/// <summary>
/// The shipment order request model for the ingestion APIs.
/// <remarks>
/// <para>This model inherits common order properties from the base OrderRequest model.</para>
/// </remarks>
/// </summary>
public class AddShipmentOrderRequest : OrderRequest
{
    [JsonIgnore]
    public override ChannelType ChannelType => ChannelType.STANDARD;

    /// <summary>
    /// The list of <c>To</c> recipients for the shipment order.
    /// <remarks>
    /// <para>Each object in this array must contain a recipient's address.</para>
    /// <para>Each object in the array may optionally contain a recipient's display name.</para>
    /// </remarks>
    /// </summary>
    [Required, MinLength(1)]
    public required List<AddressInfo> To { get; set; }

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
    [LogMasked]
    public string? OrderTitle { get; set; }
}
