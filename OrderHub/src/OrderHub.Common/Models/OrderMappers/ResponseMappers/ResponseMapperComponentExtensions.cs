using OrderHub.Common.Utilities;
using OrderHub.Contracts.Access;
using OrderHub.Contracts.Common;
using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Common.Models.OrderMappers.ResponseMappers;

public static class ResponseMapperComponentExtensions
{
    public static Platform? ToPlatformResponseModel(this Components.Platform? platform)
    {
        if (platform == null) return null;

        return new Platform
        {
            Id = (PlatformId)platform.Id,
            OperationId = platform.OperationId,
            CustomerId = platform.CustomerId,
            CustomerName = platform.CustomerName,
            AgentId = platform.AgentId,
            AgentName = platform.AgentName,
            TrackingId = platform.TrackingId,
        };
    }

    public static OrderMetadata? ToOrderMetadataResponseModel(this Components.OrderMetadata? orderMetadata, string encodedS3OrderKey)
    {
        if (orderMetadata == null) return null;

        return new OrderMetadata
        {
            MediaIds = orderMetadata.MediaIds.Count > 0 ? orderMetadata.MediaIds : null,
            ContentLength = orderMetadata.ContentLength,
            VisibleContentLength = orderMetadata.VisibleContentLength,
            PlainTextContentLength = orderMetadata.PlainTextContentLength,
            FullContentKey = encodedS3OrderKey
        };
    }

    public static string ToFormattedToRecipients(this List<AddressInfo> toAddressInfos)
    {
        return string.Join(", ", toAddressInfos.Select(a =>
            string.IsNullOrEmpty(a.Name)
                ? a.Address
                : $"\"{a.Name}\" <{a.Address}>"));
    }
}
