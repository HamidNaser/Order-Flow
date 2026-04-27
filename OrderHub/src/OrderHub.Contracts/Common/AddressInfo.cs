using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Common;

public class AddressInfo
{
    /// <summary>
    /// The address identifier for a party (sender or recipient) tied to the order.
    /// </summary>
    /// <example>ORD-ADDR-12345</example>
    [Required]
    [AddressValidation]
    public required string Address { get; set; }

    /// <summary>
    /// An optional display name for a party (sender or recipient) tied to the order.
    /// <remarks>
    /// <para>The display name provides a human-readable label for the address identifier.</para>
    /// <para>Examples with <c>Name</c> in bold:</para>
    /// <list>
    ///   <item><description><b>John Doe</b></description></item>
    ///   <item><description><b>Smith, Jane (CAI - Atlanta)</b></description></item>
    /// </list>
    /// <para>This value should NOT include wrapping double quotes. Please omit them as this will be handled automatically.</para>
    /// </remarks>
    /// </summary>
    /// <example>John Doe</example>
    public string? Name { get; set; }
}
