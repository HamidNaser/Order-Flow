using OrderHub.Contracts.Common.Enums;
using MongoDB.Bson;
using Priority = OrderHub.Common.Models.Components.Priority;
using MerchantName = OrderHub.Common.Models.Components.MerchantName;

namespace OrderHub.Common.Services;

public class S3OrderKey
{
    public required Priority Priority { get; init; }
    public required MerchantName MerchantName { get; init; }
    public required ChannelType ChannelType { get; init; }
    public required string SourceOrderId {get; init;}
    public required string OrderId { get; init; }

    public static string GenerateDuplicateProtectionPrefix(Priority priority, MerchantName merchantName, ChannelType channelType, string sourceOrderId)
    {
        return $"{priority.ToString()}/{merchantName.ToString()}/{channelType.ToString()}/{sourceOrderId}";
    }

    public string ToKeyString()
    {
        return $"{Priority.ToString()}/{MerchantName.ToString()}/{ChannelType.ToString()}/{SourceOrderId}/{OrderId}";
    }

    /// <summary>
    /// Try and parse S3 Key from string into component parts. Guaranteed non-null if returns true.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="parsedKey"></param>
    /// <returns></returns>
    public static bool TryParse(string key, out S3OrderKey? parsedKey)
    {
        parsedKey = null;

        var segments = key.Split('/');
        if (segments.Length != 5)
        {
            return false;
        }

        if (!Enum.TryParse<Priority>(segments[0], true, out var priority))
        {
            return false;
        }

        if (!Enum.TryParse<MerchantName>(segments[1], true, out var merchantName))
        {
            return false;
        }

        if (!Enum.TryParse<ChannelType>(segments[2], true, out var channelType))
        {
            return false;
        }

        var sourceOrderId = segments[3];
        if (string.IsNullOrWhiteSpace(sourceOrderId))
        {
            return false;
        }

        var orderId = segments[4];
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return false;
        }

        // We'll work with IDs as strings internally, but should still confirm they are valid ObjectIds.
        if (!ObjectId.TryParse(orderId, out _))
        {
            return false;
        }

        parsedKey = new S3OrderKey
        {
            Priority = priority,
            MerchantName = merchantName,
            ChannelType = channelType,
            SourceOrderId = sourceOrderId,
            OrderId = orderId
        };

        return true;
    }
}
