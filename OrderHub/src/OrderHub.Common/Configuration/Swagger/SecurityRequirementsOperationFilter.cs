using OrderHub.Common.Configuration.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrderHub.Common.Configuration.Swagger;

/// <summary>
/// This is the per-endpoint (operation) level "lock" in Swagger.
/// <remarks>
/// <para>
/// It makes sure each endpoint pulls the correct policy by checking attributes on the method,
/// then on declaring type, which covers controller level authorize policies.
/// </para>
/// </remarks>
/// </summary>
public class SecurityRequirementsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authorizeAttribute =
            context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .FirstOrDefault()
            ?? context.MethodInfo.DeclaringType?
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .FirstOrDefault();

        if (authorizeAttribute?.Policy == null)
        {
            return;
        }

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            CreateSecurityRequirement(
                authorizeAttribute.Policy == ApiKeyAuthenticationDefaults.AuthorizationPolicy
                    ? authorizeAttribute.Policy
                    : BridgeOAuthSettings.RawTokenAuthorizationPolicy
            )
        };
    }

    private static OpenApiSecurityRequirement CreateSecurityRequirement(string policyId) => new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = policyId,
                    Type = ReferenceType.SecurityScheme
                }
            },
            []
        }
    };
}
