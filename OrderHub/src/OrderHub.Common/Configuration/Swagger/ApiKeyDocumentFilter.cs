using OrderHub.Common.Configuration.Auth;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrderHub.Common.Configuration.Swagger;

/// <summary>
/// Removes the API Key security scheme from non-V0 API versions.
/// API Key authentication should only appear in V0 Swagger documents.
/// </summary>
public class ApiKeyDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Only keep API Key security scheme for V0 versions
        var isV0 = context.ApiDescriptions
            .Any(desc => desc.GetApiVersion()?.MajorVersion == 0);

        if (!isV0 && swaggerDoc.Components?.SecuritySchemes != null)
        {
            // Remove API Key from non-V0 versions
            swaggerDoc.Components.SecuritySchemes.Remove(ApiKeyAuthenticationDefaults.AuthorizationPolicy);
        }
    }
}
