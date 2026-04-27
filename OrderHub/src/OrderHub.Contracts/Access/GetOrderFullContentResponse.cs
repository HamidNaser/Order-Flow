using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Access;

/// <summary>
/// Response model for order content retrieval endpoint.
/// Contains the full content of a order retrieved by encoded S3 key.
/// </summary>
public class GetOrderFullContentResponse
{
    /// <summary>
    /// The primary content of the order.
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>STANDARD</c> - standard order content</description></item>
    ///   <item><description><c>DIGITAL</c> - digital order content</description></item>
    ///   <item><description><c>DIRECT</c> - direct order content</description></item>
    /// </list>
    /// </remarks>
    /// </summary>
    [Required]
    public required string Content { get; set; }
}
