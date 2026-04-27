namespace OrderHub.Common.Configuration.Clients;

public class ClientSettings
{
    public required string BaseAddress { get; init; }
    public int TimeoutSeconds { get; init; } = 10;
    public required string OAuthProvider { get; init; }
}
