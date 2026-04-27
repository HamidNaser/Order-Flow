using Serilog;

namespace OrderGateway.Common.Clients.CloudContent.V1;

public partial class CloudContentClient : ICloudContentClient
{
    /// <summary>
    /// Retrieves the text content for the provided key. Returns null on 404.
    /// This was added as a convenience method due to the fact that the generated method didn't have a return value (improper swagger-ui setup on Cloud Content side).
    /// </summary>
    /// <param name="key">The string key for the Cloud Content item. Must be in the form of "bucket/objectid"</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The Cloud Content text as a string. When not found, returns null.</returns>
    /// <exception cref="ArgumentException">When the key is null or whitespace</exception>
    public async Task<string?> TextGETContentAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key must be provided", nameof(key));

        if (key.Contains('"'))
        {
            Log.Debug("Stripping double quotes from content key. Original key: {Key}", key);
            key = key.Replace("\"", string.Empty);
        }

        var client_ = _httpClient; // existing generated field
        using var request_ = new HttpRequestMessage(HttpMethod.Get, $"Text/{key}");
        PrepareRequest(client_, request_, request_.RequestUri!.ToString());

        var response_ = await client_.SendAsync(request_, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            ProcessResponse(client_, response_);
            var status_ = (int)response_.StatusCode;
            if (status_ == 200)
            {
                // Raw string body expected
                return await response_.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            if (status_ == 404)
            {
                return null; // Not found
            }

            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new CloudContentApiV1ClientException("Unexpected status code", status_, responseData_, response_.Headers.ToDictionary(h => h.Key, h => h.Value), null);
        }
        finally
        {
            response_.Dispose();
        }
    }
}
