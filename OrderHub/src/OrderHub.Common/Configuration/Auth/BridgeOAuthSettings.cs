namespace OrderHub.Common.Configuration.Auth;

public class BridgeOAuthSettings
{
    public const string AuthenticationScheme = "BridgeOAuthScheme";
    public const string IngestStandardOrdersPolicy = "IngestStandardOrdersPolicy";
    public const string IngestStandardCustomerPolicy = "IngestStandardCustomerPolicy";
    public const string IngestExpressOrdersPolicy = "IngestExpressOrdersPolicy";
    public const string ReadOrdersPolicy = "ReadOrdersPolicy";
    public const string RawTokenAuthorizationPolicy = "RawToken";

    public const string BridgeScopeClaim = "http://schemas.microsoft.com/identity/claims/scope";

    public required string Audience { get; set; }

    public required string Authority { get; set; }

    public required BridgeOAuthPolicy[] Policies { get; set; } = [];
}
