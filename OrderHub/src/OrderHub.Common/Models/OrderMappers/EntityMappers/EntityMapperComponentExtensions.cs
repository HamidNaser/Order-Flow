using OrderHub.Common.Models.Components;
using OrderHub.Common.Repositories.Entities;

namespace OrderHub.Common.Models.OrderMappers.EntityMappers;

public static class EntityMapperComponentExtensions
{
    public static DateTime GetOrderDateUtc(this ChannelOrder order)
    {
        return order.OrderFulfilledDate?.UtcDateTime
               ?? order.OrderPlacedDate.UtcDateTime;
    }

    public static MerchantEntity ToMerchantEntity(this Merchant merchant) => new()
    {
        Name = merchant.Name.ToString(),
        OrderId = merchant.OrderId,
        SourceApplication = merchant.SourceApplication
    };

    public static PlatformEntity? ToPlatformEntity(this Platform? platform)
    {
        if (platform == null) return null;

        return new PlatformEntity
        {
            Id = platform.Id.ToString(),
            OperationId = platform.OperationId,
            CustomerId = platform.CustomerId,
            CustomerName = platform.CustomerName,
            AgentId = platform.AgentId,
            AgentName = platform.AgentName,
            TrackingId = platform.TrackingId,
        };
    }

    public static EndpointsEntity ToEndpointsEntity(this Components.Endpoints endpoints) => new()
    {
        From = endpoints.From,
        To = endpoints.To,
    };

    public static Merchant ToMerchantInternalModel(this MerchantEntity merchantEntity)
    {
        var isValidMerchantName = Enum.TryParse<MerchantName>(merchantEntity.Name, out var merchantName);

        if (!isValidMerchantName)
        {
            throw new ArgumentException($"Invalid MerchantName on entity: {merchantEntity.Name}", nameof(merchantEntity));
        }

        return new Merchant
        {
            Name = merchantName,
            OrderId = merchantEntity.OrderId,
            SourceApplication = merchantEntity.SourceApplication
        };
    }

    public static Platform? ToPlatformInternalModel(this PlatformEntity? platformEntity)
    {
        if (platformEntity == null) return null;

        var isValidPlatformId = Enum.TryParse<PlatformId>(platformEntity.Id, out var platformId);

        if (!isValidPlatformId)
        {
            throw new ArgumentException($"Invalid PlatformId on entity: {platformEntity.Id}", nameof(platformEntity));
        }

        return new Platform
        {
            Id = platformId,
            OperationId = platformEntity.OperationId,
            CustomerId = platformEntity.CustomerId,
            CustomerName = platformEntity.CustomerName,
            AgentId = platformEntity.AgentId,
            AgentName = platformEntity.AgentName,
            TrackingId = platformEntity.TrackingId,
        };
    }

    public static Components.Endpoints ToEndpointsInternalModel(this EndpointsEntity endpointsEntity) => new()
    {
        From = endpointsEntity.From,
        To = endpointsEntity.To,
    };

    public static OrderMetadataEntity? ToOrderMetadataEntity(this OrderMetadata? orderMetadata)
    {
        if (orderMetadata == null) return null;

        return new OrderMetadataEntity
        {
            MediaIds = orderMetadata.MediaIds,
            ContentLength = orderMetadata.ContentLength,
            VisibleContentLength = orderMetadata.VisibleContentLength,
            PlainTextContentLength = orderMetadata.PlainTextContentLength,
        };
    }

    public static OrderMetadata? ToOrderMetadataInternalModel(this OrderMetadataEntity? orderMetadataEntity)
    {
        if (orderMetadataEntity == null) return null;

        return new OrderMetadata
        {
            MediaIds = orderMetadataEntity.MediaIds,
            ContentLength = orderMetadataEntity.ContentLength,
            VisibleContentLength = orderMetadataEntity.VisibleContentLength,
            PlainTextContentLength = orderMetadataEntity.PlainTextContentLength,
        };
    }
}
