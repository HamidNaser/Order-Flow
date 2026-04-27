using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Ingest;

/// <summary>
/// The order response model for the ingestion APIs.
/// <remarks>
/// <para>This model returns the unique identifier for the order.</para>
/// </remarks>
/// </summary>
public class OrderResponse
{
    /// <summary>
    /// The unique identifier for the order.
    /// </summary>
    /// <example>68e433dd6d302b9378615fd9</example>
    [Required]
    public required string Id { get; set; }
}
