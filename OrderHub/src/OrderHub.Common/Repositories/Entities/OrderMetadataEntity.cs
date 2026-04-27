using MongoDB.Bson.Serialization.Attributes;

namespace OrderHub.Common.Repositories.Entities;

/// <summary>
/// MongoDB entity for content metadata with conditional serialization based on field values.
/// </summary>
public class OrderMetadataEntity
{
    /// <summary>
    /// A list of media identifiers associated with the order content.
    /// </summary>
    [BsonElement]
    public required List<string> MediaIds { get; set; } = [];

    /// <summary>
    /// Determines whether MediaIds should be serialized to MongoDB.
    /// </summary>
    /// <returns>True if MediaIds has at least one element.</returns>
    public bool ShouldSerializeMediaIds() => MediaIds.Count > 0;

    /// <summary>
    /// The total length of the raw content string in characters.
    /// </summary>
    [BsonElement, BsonIgnoreIfDefault]
    public required int ContentLength { get; set; }

    /// <summary>
    /// The length of visible content after processing, calculated using text elements for proper multi-byte character handling.
    /// </summary>
    [BsonElement, BsonIgnoreIfDefault]
    public required int VisibleContentLength { get; set; }

    /// <summary>
    /// The length of content after markup stripping for standard orders. Null for non-standard channels.
    /// </summary>
    [BsonElement("ContentLengthNoHtml")]
    public int? PlainTextContentLength { get; set; }

    /// <summary>
    /// Determines whether PlainTextContentLength should be serialized to MongoDB.
    /// </summary>
    /// <returns>True if PlainTextContentLength has a value and is greater than 0.</returns>
    public bool ShouldSerializePlainTextContentLength() => PlainTextContentLength.HasValue && PlainTextContentLength.Value > 0;
}
