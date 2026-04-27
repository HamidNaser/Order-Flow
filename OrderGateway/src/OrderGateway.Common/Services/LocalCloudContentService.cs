using Microsoft.Extensions.Configuration;
using Serilog;

namespace OrderGateway.Common.Services;

/// <summary>
/// A local stub for <see cref="ICloudContentService"/> that returns content from app configuration.
/// Used in localstack/local environments where the real CloudContent service is not reachable.
/// Activated when the "LocalCloudContent" configuration section is present.
/// </summary>
public sealed class LocalCloudContentService : ICloudContentService
{
    private readonly Dictionary<string, string> _content;

    public LocalCloudContentService(IConfiguration configuration)
    {
        _content = configuration.GetSection("LocalCloudContent").Get<Dictionary<string, string>>() ?? new();
        Log.Information("LocalCloudContentService initialized with {Count} seeded key(s)", _content.Count);
    }

    public Task<string?> ReadContentAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must be provided", nameof(key));

        _content.TryGetValue(key, out var value);

        if (value is null)
            Log.Warning("LocalCloudContentService: No seeded content for key {Key}", key);

        return Task.FromResult(value);
    }
}
