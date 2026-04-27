using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Common;

public class Endpoints
{
    /// <summary>
    /// The recipient's phone number for the text or call order.
    /// <remarks>
    /// <para>Normalized using E.164 format.</para>
    /// </remarks>
    /// </summary>
    /// <example>+13234345676</example>
    [Required]
    public required string To { get; set; }

    /// <summary>
    /// The sender's phone number for the text or call order.
    /// <remarks>
    /// <para>Normalized using E.164 format.</para>
    /// </remarks>
    /// </summary>
    /// <example>+13234345676</example>
    [Required]
    public required string From { get; set; }
}
