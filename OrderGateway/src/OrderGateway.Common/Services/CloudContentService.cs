using OrderGateway.Common.Clients.CloudContent.V1;
using Serilog;

namespace OrderGateway.Common.Services;

public sealed class CloudContentService(ICloudContentClient client, ILogger logger) : ICloudContentService
{
    public async Task<string?> ReadContentAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must be provided", nameof(key));
        }

        try
        {
            var content = await client.TextGETContentAsync(key, ct);
            return content;
        }
        catch (CloudContentApiV1ClientException ex) when (ex.StatusCode == 404)
        {
            logger.Warning(ex, "CloudContentService: Content not found for key {Key}. Returning null.", key);
            return null; // 404 path
        }
        catch (CloudContentApiV1ClientException ex) when (ex.StatusCode == 400)
        {
            logger.Warning(ex, "CloudContentService: Bad request for key {Key}. Returning null.", key);
            return null; // treat as not found
        }
    }
}
