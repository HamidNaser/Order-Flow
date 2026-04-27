using Serilog;

namespace OrderGateway.Common.Helpers;

internal static class VideoMediaParser
{
    private const int HighMediaIdThreshold = 5;

    internal static ICollection<string>? ParseVideoMediaIds(string? videoMedia, string eventType)
    {
        if (string.IsNullOrWhiteSpace(videoMedia))
        {
            return null;
        }

        try
        {
            var mediaIds = videoMedia
                .Split(',')
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            if (mediaIds.Count == 0)
            {
                return null;
            }

            if (mediaIds.Count > HighMediaIdThreshold)
            {
                Log.Information("{EventType} VideoMedia contains {Count} items: {VideoMedia}", eventType, mediaIds.Count, videoMedia);
            }

            return mediaIds;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{EventType} failed to parse VideoMedia: {VideoMedia}", eventType, videoMedia);
            return null;
        }
    }
}
