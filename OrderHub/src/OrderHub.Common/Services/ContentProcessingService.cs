using System.Globalization;
using OrderHub.Common.Configuration.Channels;
using OrderHub.Common.Services.Utils;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Common.Services;

/// <summary>
/// Result object containing both the truncated content preview and the full visible content length.
/// </summary>
/// <param name="OrderSummary">The truncated content preview (max 300 characters by default).</param>
/// <param name="ContentLength">The length of the original content string (before any markup stripping or processing).</param>
/// <param name="VisibleContentLength">The full length of visible content using StringInfo.LengthInTextElements for accurate multi-byte character counting.</param>
/// <param name="PlainTextContentLength">The length of content after markup stripping (only populated for STANDARD channel, 0 for other channels).</param>
public record ContentProcessingResult(string OrderSummary, int ContentLength, int VisibleContentLength, int PlainTextContentLength);

public interface IContentProcessingService
{
    ContentProcessingResult ProcessContent(ChannelType channelType, string content);
}

public class ContentProcessingService(OrderSummaryConfig config, IHtmlTextExtractorService htmlTextExtractor) : IContentProcessingService
{
    public ContentProcessingResult ProcessContent(ChannelType channelType, string content)
    {
        // Calculate the original content length before any processing
        var contentLength = string.IsNullOrWhiteSpace(content) ? 0 : content.Length;

        // Extract text based on channel type (strips HTML for STANDARD)
        var extractedText = channelType switch
        {
            ChannelType.STANDARD => htmlTextExtractor.ExtractText(content),
            _ => content,
        };

        // Handle null or whitespace content early
        if (string.IsNullOrWhiteSpace(extractedText))
            return new ContentProcessingResult(string.Empty, contentLength, 0, 0);

        // Create StringInfo once for both length calculation and truncation
        var stringInfo = new StringInfo(extractedText);
        var visibleContentLength = stringInfo.LengthInTextElements;

        // Truncate for the preview if needed
        var contentPreview = visibleContentLength <= config.TruncateLength
            ? extractedText
            : stringInfo.SubstringByTextElements(0, config.TruncateLength);

        // For STANDARD, the extractedText length is the PlainTextContentLength
        var plainTextContentLength = channelType == ChannelType.STANDARD
            ? extractedText.Length
            : 0;

        return new ContentProcessingResult(contentPreview, contentLength, visibleContentLength, plainTextContentLength);
    }
}
