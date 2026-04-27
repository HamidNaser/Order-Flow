namespace OrderGateway.Common.Configuration;

public static class BrandedConfigurationFileNames
{
    public static string? GetAppSettingsFileName(string? environmentName) => environmentName?.ToLowerInvariant() switch
    {
        "localstack" => "appsettings.localstack.json",
        "qa" => "appsettings.awsordergateway.qa.json",
        "staging" => "appsettings.awsordergateway.staging.json",
        "production" => "appsettings.awsordergateway.production.json",
        _ => null
    };

    public static string GetTestSettingsFileName(string? environmentName) => environmentName?.ToLowerInvariant() switch
    {
        "qa" => "testsettings.awsordergateway.qa.json",
        "staging" => "testsettings.awsordergateway.staging.json",
        "production" => "testsettings.awsordergateway.production.json",
        not null => $"testsettings.{environmentName}.json",
        _ => throw new InvalidConfigurationException("Missing environment variable: DOTNET_ENVIRONMENT")
    };
}