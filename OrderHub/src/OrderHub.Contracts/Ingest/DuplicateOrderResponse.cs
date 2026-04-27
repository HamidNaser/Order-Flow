using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Ingest;

/// <summary>
/// The duplicate order response model for the ingestion APIs. This is returned when an ingestion request comes in that matches an already ingested order.
/// <remarks>
/// <para>Matches are determined by order merchant information, namely the Channel Type, Merchant's Name, and Merchant's Order ID.</para>
/// </remarks>
/// </summary>
public class DuplicateOrderResponse
{
    /// <summary>
    /// The unique identifier for the existing order this request was a duplicate of
    /// </summary>
    /// <example>68e433dd6d302b9378615fd9</example>
    [Required]
    public required string Id { get; set; }
}
