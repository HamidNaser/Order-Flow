namespace OrderHub.Common.Configuration.Clients;

public class ApiKeyClientSettings : ClientSettings
{
    public string HeaderName { get; init; } = "x-api-key";
    public required string ApiKey { get; init; }
}
