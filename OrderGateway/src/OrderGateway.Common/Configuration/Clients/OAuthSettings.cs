namespace OrderGateway.Common.Configuration.Clients;

public class OAuthSettings
{
    public string AuthorityUrl { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
}
