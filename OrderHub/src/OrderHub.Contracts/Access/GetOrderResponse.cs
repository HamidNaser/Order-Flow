using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OrderHub.Contracts.Common;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Contracts.Access;

/// <summary>
/// The base order response model.
/// <remarks>
/// <para>This model has common order properties shared across all channel types.</para>
/// <para>A discriminator field <c>ChannelType</c> correlates the polymorphic inheritance for the following.</para>
/// <list type="bullet">
///   <item><description><c>STANDARD</c> - GetShipmentResponse</description></item>
///   <item><description><c>DIGITAL</c> - GetDigitalResponse</description></item>
/// </list>
/// </remarks>
/// </summary>
[PolymorphicDiscriminator]
[JsonPolymorphic(TypeDiscriminatorPropertyName = ChannelTypeConstants.DiscriminatorName)]
[JsonDerivedType(typeof(GetShipmentResponse), ChannelTypeConstants.StandardDiscriminatorValue)]
[JsonDerivedType(typeof(GetDigitalResponse), ChannelTypeConstants.DigitalDiscriminatorValue)]
public abstract class GetOrderResponse
{
    /// <summary>
    /// The unique identifier for the order.
    /// </summary>
    /// <example>68e433dd6d302b9378615fd9</example>
    [Required]
    public required string OrderId { get; set; }

    /// <summary>
    /// The unique identifier for the customer (connected customer) involved in this order.
    /// </summary>
    /// <example>bf11a6b9-b991-4360-ba46-f82e23a3273d</example>
    [Required]
    public required string CustomerId { get; set; }

    /// <summary>
    /// The full name of the customer pertaining to this order.
    /// </summary>
    /// <example>John Smith</example>
    public string? CustomerName { get; set; }

    /// <summary>
    /// The unique identifier for the common user (bridge) involved in this order.
    /// </summary>
    /// <example>BRIDGE_ID123</example>
    public string? AgentId { get; set; }

    /// <summary>
    /// The full name of the common user.
    /// </summary>
    /// <example>Jane Smith</example>
    public string? AgentName { get; set; }

    /// <summary>
    /// The unique identifier for the common organization (store).
    /// </summary>
    /// <example>bf11a6b9-b991-4360-ba46-f82e23a3273d, CoOrgTestId1, CoOrgTestId2</example>
    [Required]
    public required string StoreId { get; set; }

    /// <summary>
    /// The unique identifier for the (common organization) tenant.
    /// </summary>
    /// <example>TENANT123</example>
    public string? TenantId { get; set; }

    /// <summary>
    /// The primary content of the order truncated to first 300 visible characters.
    /// <remarks>
    /// <para>Common content field between all order types.</para>
    /// <list type="bullet">
    ///   <item><description><c>STANDARD</c> - standard order content (with HTML removed)</description></item>
    ///   <item><description><c>DIGITAL</c> - digital order content</description></item>
    /// </list>
    /// </remarks>
    /// </summary>
    public string? OrderSummary { get; set; }

    /// <summary>
    /// The date and time (with offset) when the order was sent.
    /// </summary>
    /// <example>2024-01-15T09:00:00-05:00 or 2024-01-15T10:30:00Z</example>
    [Required]
    public required DateTimeOffset OrderPlacedDateUtc { get; set; }

    /// <summary>
    /// The date and time (with offset) when the order was successfully delivered.
    /// <br/>
    /// <remarks>
    /// If FulfillmentStatus is set to <c>SUCCESS</c>, this field becomes required and must be provided.
    /// For other statuses, this field should be omitted or null.
    /// </remarks>
    /// </summary>
    /// <example>2024-01-15T09:00:00-05:00 or 2024-01-15T10:30:15Z</example>
    public DateTimeOffset? OrderFulfilledDateUtc { get; set; }

    [Required]
    public required OrderFlowType OrderFlow { get; set; }

    [Required]
    public required Merchant Merchant { get; set; }

    public Platform? Platform { get; set; }

    [Required]
    public required FulfillmentStatus FulfillmentStatus { get; set; }

    [Required]
    public required Priority Priority { get; set; }

    /// <summary>
    /// Metadata about the content of the order, including media references and content length metrics.
    /// </summary>
    public OrderMetadata? OrderMetadata { get; set; }
}
