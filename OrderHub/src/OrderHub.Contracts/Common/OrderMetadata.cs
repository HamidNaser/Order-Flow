using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts.Common;

/// <summary>
/// Represents metadata about the content of a order, including media references and content length metrics.
/// </summary>
public class OrderMetadata
{
    /// <summary>
    /// A list of media identifiers associated with the order content.
    /// </summary>
    /// <example>["media123", "media456"]</example>
    public List<string>? MediaIds { get; set; }

    /// <summary>
    /// The total length of the raw content string in characters.
    /// </summary>
    /// <example>150</example>
    [Required]
    public required int ContentLength { get; set; }

    /// <summary>
    /// The length of visible content after processing, calculated using text elements for proper multi-byte character handling.
    /// </summary>
    /// <example>125</example>
    [Required]
    public required int VisibleContentLength { get; set; }

    /// <summary>
    /// The length of content after markup stripping for standard orders. Null for non-standard channels.
    /// </summary>
    /// <example>125</example>
    public int? PlainTextContentLength { get; set; }

    /// <summary>
    /// </summary>
    /// <example>VFJBTlNBQ1RJT05BTC9WSU4vRU1BSUwvc3JjMTIzLzY0ZjEyMzQ1Njc4OTBhYmNkZWYxMjM0NQ</example>
    public string? FullContentKey { get; set; }
}
