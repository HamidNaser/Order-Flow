namespace OrderGateway.Common.Clients.CloudContent.V1;

/// <summary>
/// Adds convenience methods missing due to incorrect swagger (GET Text/{key} returns raw string body).
/// </summary>
public partial interface ICloudContentClient
{
    /// <summary>
    /// Retrieves the text content for the provided key. Returns null on 404.
    /// </summary>
    Task<string?> TextGETContentAsync(string key, CancellationToken cancellationToken = default);
}
