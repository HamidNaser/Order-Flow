namespace OrderHub.Common.Configuration.Aws;

/// <summary>
/// AWS connection configuration for LocalStack or custom endpoints.
/// When this configuration section is present in appsettings, the application will connect to the specified endpoint.
/// When absent, AWS SDK will use default behavior (connect to real AWS).
/// </summary>
public class AwsConnectionOptions
{
    /// <summary>
    /// Service URL for LocalStack or custom AWS endpoint.
    /// Example: "http://localhost:4566" for LocalStack
    /// Required when AwsConnectionOptions is configured.
    /// </summary>
    public required string ServiceUrl { get; init; }

    /// <summary>
    /// AWS region for authentication.
    /// Defaults to "us-east-1" if not specified.
    /// </summary>
    public string Region { get; init; } = "us-east-1";
}
