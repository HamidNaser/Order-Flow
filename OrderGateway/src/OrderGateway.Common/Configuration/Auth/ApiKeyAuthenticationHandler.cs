using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderGateway.Common.Configuration.Auth;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<ApiKeyAuthenticationOptions>(
    options,
    logger,
    encoder
)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var apiKeyHeaderValues) == false)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (apiKeyHeaderValues.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Request was received with an invalid {ApiKeyAuthenticationDefaults.HeaderName} header configuration"));
        }

        var apiKey = apiKeyHeaderValues.Single();

        if (
            string.IsNullOrWhiteSpace(apiKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(apiKey),
                Encoding.UTF8.GetBytes(Options.ApiKey))
        )
        {
            return Task.FromResult(AuthenticateResult.Fail($"Request was received with an invalid {ApiKeyAuthenticationDefaults.HeaderName} header"));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "ApiKeyUser") };
        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var identities = new List<ClaimsIdentity> { identity };
        var principal = new ClaimsPrincipal(identities);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
