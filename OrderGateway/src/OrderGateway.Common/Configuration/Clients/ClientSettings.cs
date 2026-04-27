namespace OrderGateway.Common.Configuration.Clients;

public class ClientSettings
{
    public string BaseAddress { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 10;
    public string OAuthProvider { get; init; } = string.Empty;
}
