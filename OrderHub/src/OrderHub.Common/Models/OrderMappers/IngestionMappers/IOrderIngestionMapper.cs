using OrderHub.Common.Models.Components;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;

namespace OrderHub.Common.Models.OrderMappers.IngestionMappers;

public interface IOrderIngestionMapper
{
    ChannelOrder ToInternalModel(
        OrderRequest request,
        string orderId,
        ContentProcessingResult contentProcessingResult,
        Priority priority
    );
}
