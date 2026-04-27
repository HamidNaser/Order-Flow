using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Contracts.Ingest;

/// <summary>
/// The text order request model for the ingestion APIs.
/// <remarks>
/// <para>This model inherits common order properties from the base OrderRequest model.</para>
/// <para>A text order requires Content.</para>
/// </remarks>
/// </summary>
public class
AddDigitalOrderRequest : OrderRequest
{
    [JsonIgnore]
    public override ChannelType ChannelType => ChannelType.DIGITAL;

    /// <summary>
    /// The recipient's phone number for the text order.
    /// <remarks>
    /// <para>Any valid (not reserved/special) phone number. See examples.</para>
    /// </remarks>
    /// </summary>
    /// <example>(616) 323-4454 or +13234345676 or 616-676-9087 or +1 616-676-9087</example>
    [Required]
    [PhoneNumberValidation]
    public required string ToPhoneNumber { get; set; }

    /// <summary>
    /// The sender's phone number for the text order.
    /// <remarks>
    /// <para>Any valid (not reserved/special) phone number. See examples.</para>
    /// </remarks>
    /// </summary>
    /// <example>(616) 323-4454 or +13234345676 or 616-676-9087 or +1 616-676-9087</example>
    [Required]
    [PhoneNumberValidation]
    public required string FromPhoneNumber { get; set; }
}
