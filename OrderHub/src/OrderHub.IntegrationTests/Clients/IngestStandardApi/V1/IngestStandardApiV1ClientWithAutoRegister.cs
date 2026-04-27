using OrderHub.Common.Models.Components;
using OrderHub.Common.Services;
using OrderHub.Contracts;
using OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts;
using ChannelType = OrderHub.Contracts.Common.Enums.ChannelType;
using MerchantName = OrderHub.Common.Models.Components.MerchantName;

namespace OrderHub.IntegrationTests.Clients.IngestStandardApi.V1;

/// <summary>
/// Auto-register wrapper for IngestStandardApiV1Client that automatically registers created orders for cleanup.
/// </summary>
public class IngestStandardApiV1ClientWithAutoRegister : IngestStandardApiV1Client
{
    private readonly ApiTestsFixture _fixture;

    public IngestStandardApiV1ClientWithAutoRegister(string baseUrl, HttpClient httpClient, ApiTestsFixture fixture)
        : base(baseUrl, httpClient)
    {
        _fixture = fixture;
    }

    public override async Task<HttpResponse<OrderResponse>> AddDigitalOrderAsync(AddDigitalOrderRequest? body, CancellationToken cancellationToken)
    {
        var response = await base.AddDigitalOrderAsync(body, cancellationToken);
        RegisterIfSuccessful(body, response, ChannelTypeConstants.DigitalDiscriminatorValue);
        return response;
    }

    public override async Task<HttpResponse<OrderResponse>> AddShipmentOrderAsync(AddShipmentOrderRequest? body, CancellationToken cancellationToken)
    {
        var response = await base.AddShipmentOrderAsync(body, cancellationToken);
        RegisterIfSuccessful(body, response, ChannelTypeConstants.StandardDiscriminatorValue);
        return response;
    }

    private void RegisterIfSuccessful(OrderRequest? request, HttpResponse<OrderResponse> response, string channelType)
    {
        if (response.StatusCode == 202 && !string.IsNullOrEmpty(response.Result?.Id) && !string.IsNullOrEmpty(request?.Merchant.Name.ToString()))
        {
            var channelTypeEnum = Enum.Parse<ChannelType>(channelType);
            var merchantNameEnum = (MerchantName)request.Merchant.Name;

            var s3Key = new S3OrderKey
            {
                Priority = Priority.STANDARD,
                MerchantName = merchantNameEnum,
                ChannelType = channelTypeEnum,
                SourceOrderId = request.Merchant.OrderId,
                OrderId = response.Result.Id
            };

            _fixture.RegisterOrder(s3Key, request.StoreId);
        }
    }
}
