using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Access;

/// <summary>
/// The full order response model.  Contains all order details and its full content.
/// </summary>
public class GetFullOrderResponse
{
    [Required]
    public required GetOrderResponse Order { get; set; }

    /// <summary>
    /// The primary content of the order. Common content field between all order types.
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>STANDARD</c> - standard order content</description></item>
    ///   <item><description><c>DIGITAL</c> - digital order content</description></item>
    ///   <item><description><c>DIRECT</c> - direct order content</description></item>
    /// </list>
    /// </remarks>
    /// </summary>
    public string? Content { get; set; }
}
