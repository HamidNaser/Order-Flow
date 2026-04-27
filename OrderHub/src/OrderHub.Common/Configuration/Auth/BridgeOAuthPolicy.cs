namespace OrderHub.Common.Configuration.Auth;

public class BridgeOAuthPolicy
{
    public required string Name { get; set; }

    public required string[] Scopes { get; set; } = [];
}
