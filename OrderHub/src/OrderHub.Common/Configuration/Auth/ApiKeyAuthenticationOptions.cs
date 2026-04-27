using Microsoft.AspNetCore.Authentication;

namespace OrderHub.Common.Configuration.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public bool AllowAnonymous { get; init; }
}
