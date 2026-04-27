using OrderHub.Common.Models.Components;

namespace OrderHub.Common.Models.OrderMappers.IngestionMappers;

public static class IngestionMapperComponentExtensions
{
    public static Merchant ToMerchantInternalModel(this OrderHub.Contracts.Common.Merchant merchant)
    {
        return new Merchant
        {
            Name = (MerchantName)merchant.Name,
            OrderId = merchant.OrderId,
            SourceApplication = merchant.SourceApplication
        };
    }

    public static Platform? ToPlatformInternalModel(this OrderHub.Contracts.Common.Platform? platform)
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
}
