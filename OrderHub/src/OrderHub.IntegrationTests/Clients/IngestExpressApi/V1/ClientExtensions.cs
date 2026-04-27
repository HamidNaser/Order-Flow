using OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace OrderHub.IntegrationTests.Clients.IngestExpressApi.V1;

public partial class IngestExpressApiV1Client
{
    public virtual async Task<HttpResponse<OrderResponse>> CustomAddShipmentOrderOverrideMerchantNameAsync(AddShipmentOrderRequest? body, string invalidMerchantName)
    {
        var client_ = _httpClient;
        var disposeClient_ = false;
        try
        {
            using (var request_ = new HttpRequestMessage())
            {
                var json_ = JsonConvert.SerializeObject(body, JsonSerializerSettings);

                var requestObject = JObject.Parse(json_);
                var sourceToken = requestObject["merchant"];
                if (sourceToken != null)
                {
                    sourceToken["name"] = JToken.FromObject(invalidMerchantName);
                }
                var modifiedRequestJson = requestObject.ToString();

                var content_ = new StringContent(modifiedRequestJson);
                content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/vnd.order.v1+json");
                request_.Content = content_;
                request_.Method = new HttpMethod("POST");
                request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/vnd.order.v1+json"));

                var urlBuilder_ = new StringBuilder();
                if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
                // Operation Path: "api/order/shipment"
                urlBuilder_.Append("api/order/shipment");

                PrepareRequest(client_, request_, urlBuilder_);

                var url_ = urlBuilder_.ToString();
                request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

                PrepareRequest(client_, request_, url_);

                var response_ = await client_.SendAsync(request_, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None).ConfigureAwait(false);
                var disposeResponse_ = true;
                try
                {
                    var headers_ = new Dictionary<string, IEnumerable<string>>();
                    foreach (var item_ in response_.Headers)
                        headers_[item_.Key] = item_.Value;
                    if (response_.Content != null && response_.Content.Headers != null)
                    {
                        foreach (var item_ in response_.Content.Headers)
                            headers_[item_.Key] = item_.Value;
                    }

                    ProcessResponse(client_, response_);

                    var status_ = (int)response_.StatusCode;
                    if (status_ == 202)
                    {
                        var objectResponse_ = await ReadObjectResponseAsync<OrderResponse>(response_, headers_, System.Threading.CancellationToken.None).ConfigureAwait(false);
                        if (objectResponse_.Object == null)
                        {
                            throw new IngestExpressApiV1ClientException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                        }
                        return new HttpResponse<OrderResponse>(status_, headers_, objectResponse_.Object);
                    }
                    else
                        if (status_ == 400)
                        {
                            var objectResponse_ = await ReadObjectResponseAsync<HttpValidationProblemDetails>(response_, headers_, CancellationToken.None).ConfigureAwait(false);
                            if (objectResponse_.Object == null)
                            {
                                throw new IngestExpressApiV1ClientException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                            }
                            throw new IngestExpressApiV1ClientException<HttpValidationProblemDetails>("Bad Request", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
                        }
                        else
                            if (status_ == 403)
                            {
                                var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, CancellationToken.None).ConfigureAwait(false);
                                if (objectResponse_.Object == null)
                                {
                                    throw new IngestExpressApiV1ClientException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                                }
                                throw new IngestExpressApiV1ClientException<ProblemDetails>("Forbidden", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
                            }
                            else
                            {
                                var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                                throw new IngestExpressApiV1ClientException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
                            }
                }
                finally
                {
                    if (disposeResponse_)
                        response_.Dispose();
                }
            }
        }
        finally
        {
            if (disposeClient_)
                client_.Dispose();
        }
    }
}
