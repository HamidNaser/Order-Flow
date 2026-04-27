using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OrderHub.Contracts.Common;
using OrderHub.Contracts.Common.Enums;
using Destructurama.Attributed;

namespace OrderHub.Contracts.Ingest;

/// <summary>
/// The base order request model for the ingestion APIs.
/// <remarks>
/// <para>This model has common order properties shared across all channel types.</para>
/// </remarks>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = ChannelTypeConstants.DiscriminatorName)]
[JsonDerivedType(typeof(AddShipmentOrderRequest), ChannelTypeConstants.StandardDiscriminatorValue)]
[JsonDerivedType(typeof(AddDigitalOrderRequest), ChannelTypeConstants.DigitalDiscriminatorValue)]
public abstract class OrderRequest : IValidatableObject
{
    [JsonIgnore]
    public abstract ChannelType ChannelType { get; }

    /// <summary>
    /// The unique identifier for the common organization (store).
    /// </summary>
    /// <example>bf11a6b9-b991-4360-ba46-f82e23a3273d, CoOrgTestId1, CoOrgTestId2</example>
    [StringLength(50, MinimumLength = 1)]
    [Required]
    public required string StoreId { get; set; }

    /// <summary>
    /// The unique identifier for the customer (connected customer) involved in this order.
    /// </summary>
    /// <example>bf11a6b9-b991-4360-ba46-f82e23a3273d</example>
    [StringLength(50, MinimumLength = 1)]
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

    [Required]
    public required OrderFlowType OrderFlow { get; set; }

    /// <summary>
    /// The primary content of the order. Common content field between all order types.
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>STANDARD</c> - standard order content</description></item>
    ///   <item><description><c>DIGITAL</c> - digital order content</description></item>
    /// </list>
    /// </remarks>
    /// </summary>
    [LogMasked]
    public string? Content { get; set; }

    /// <summary>
    /// Optional list of Common Order Media identifiers associated with this order.
    /// <remarks>
    /// Maximum of 50 media IDs allowed, with each ID limited to 50 characters.
    /// </remarks>
    /// </summary>
    /// <example>["media123", "media456"]</example>
    [MaxLength(50)]
    public List<string>? MediaIds { get; set; }

    /// <summary>
    /// The date and time (with offset) when the order was sent.
    /// </summary>
    /// <example>2024-01-15T09:00:00-05:00 or 2024-01-15T10:30:00Z</example>
    [Required, DateTimeOffsetValidation]
    public required DateTimeOffset OrderPlacedDate { get; set; }

    /// <summary>
    /// The date and time (with offset) when the order was successfully delivered.
    /// <br/>
    /// <remarks>
    /// If FulfillmentStatus is set to <c>SUCCESS</c>, this field becomes required and must be provided.
    /// For other statuses, this field should be omitted or null.
    /// </remarks>
    /// </summary>
    /// <example>2024-01-15T09:00:00-05:00 or 2024-01-15T10:30:15Z</example>
    [DateTimeOffsetValidation]
    public DateTimeOffset? OrderFulfilledDate { get; set; }

    [Required]
    public required Merchant Merchant { get; set; }

    /// <summary>
    /// The unique identifier for the (common organization) tenant.
    /// </summary>
    /// <example>TENANT123</example>
    public string? TenantId { get; set; }

    [Required]
    public required FulfillmentStatus FulfillmentStatus { get; set; }

    public Platform? Platform { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FulfillmentStatus == FulfillmentStatus.SUCCESS && OrderFulfilledDate is null)
        {
            yield return new ValidationResult($"{nameof(OrderFulfilledDate)} is required when {nameof(FulfillmentStatus)} is '{nameof(FulfillmentStatus.SUCCESS)}'.");
        }

        if (FulfillmentStatus != FulfillmentStatus.SUCCESS && OrderFulfilledDate.HasValue)
        {
            yield return new ValidationResult($"{nameof(OrderFulfilledDate)} should be null or omitted when {nameof(FulfillmentStatus)} is not '{nameof(FulfillmentStatus.SUCCESS)}'.");
        }

        // Validate MediaIds if present
        if (MediaIds is not null)
        {
            for (int i = 0; i < MediaIds.Count; i++)
            {
                if (MediaIds[i]?.Length > 50)
                {
                    yield return new ValidationResult($"{nameof(MediaIds)}[{i}] exceeds maximum length of 50 characters.");
                }
            }
        }
    }
}
