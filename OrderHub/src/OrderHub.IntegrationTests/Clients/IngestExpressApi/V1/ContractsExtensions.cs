namespace OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts;

public partial interface IIngestExpressApiV1Client
{
    Task<HttpResponse<OrderResponse>> CustomAddShipmentOrderOverrideMerchantNameAsync(
        AddShipmentOrderRequest? body, string invalidMerchantName);
}
