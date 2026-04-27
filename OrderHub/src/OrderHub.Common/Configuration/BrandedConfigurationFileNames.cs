namespace OrderHub.Common.Configuration;

public static class BrandedConfigurationFileNames
{
    public static string? GetAppSettingsFileName(string? environmentName) => environmentName?.ToLowerInvariant() switch
    {
        "localstack" => "appsettings.localstack.json",
        "qa" => "appsettings.awsorderprocessing.qa.json",
        "staging" => "appsettings.awsorderprocessing.staging.json",
        "production" => "appsettings.awsorderprocessing.production.json",
        _ => null
    };

    public static string GetTestSettingsFileName(string? environmentName) => environmentName?.ToLowerInvariant() switch
    {
        "qa" => "testsettings.awsorderprocessing.qa.json",
        "staging" => "testsettings.awsorderprocessing.staging.json",
        "production" => "testsettings.awsorderprocessing.production.json",
        not null => $"testsettings.{environmentName}.json",
        _ => throw new InvalidOperationException("DOTNET_ENVIRONMENT is required.")
    };
}