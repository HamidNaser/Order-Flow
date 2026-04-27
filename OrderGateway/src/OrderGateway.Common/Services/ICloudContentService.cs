namespace OrderGateway.Common.Services;

/// <summary>
/// Abstraction over the Cloud Content client for simplified retrieval and 404 handling.
/// Returns null when the cloud content key is not found (404) and propagates other exceptions.
/// </summary>
public interface ICloudContentService
{
    /// <summary>
    /// Reads content text for the provided key.
    /// Returns null when the content is not found (404).
    /// </summary>
    Task<string?> ReadContentAsync(string key, CancellationToken ct = default);
}
