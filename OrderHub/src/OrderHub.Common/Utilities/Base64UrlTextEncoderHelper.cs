using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Serilog;

namespace OrderHub.Common.Utilities;

/// <summary>
/// Static utility for encoding and decoding text for safe URL transmission,
/// using Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder
/// </summary>
public static class Base64UrlTextEncoderHelper
{
    public static string Encode(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(text));
    }

    public static string Decode(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        try
        {
            return Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(text));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to decode key {Text}.", text);
            return string.Empty;
        }
    }
}
