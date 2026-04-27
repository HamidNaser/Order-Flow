using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Access;

/// <summary>
/// The text order response model.
/// <remarks>
/// <para>This model has text-specific order properties.</para>
/// <para>A discriminator field <c>ChannelType</c> on GetOrderResponse correlates the polymorphic inheritance.</para>
/// </remarks>
/// </summary>
public class GetDigitalResponse : GetOrderResponse
{
    [Required]
    public required Common.Endpoints Endpoints { get; set; }
}
