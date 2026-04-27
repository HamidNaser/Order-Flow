using OrderHub.Common.Configuration.Api;
using OrderHub.Common.Exceptions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrderHub.Common.Configuration.Swagger;

public class OrderApiVersionSupportFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var majorVersion = context.ApiDescription.GetApiVersion()?.MajorVersion;

        if (majorVersion == null)
        {
            throw new InvalidOpenApiDocException($"The operation: '{operation.OperationId}' requires an ApiVersion.");
        }

        string contentType = $"application/vnd.order.v{majorVersion}+json";

        // All requests SHOULD include the major version in the Accept header
        foreach (var openApiResponse in operation.Responses)
        {
            if (openApiResponse.Value.Content is { Count: 0 })
            {
                openApiResponse.Value.Content ??= new Dictionary<string, OpenApiMediaType>();
                openApiResponse.Value.Content[contentType] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Nullable = true
                    }
                };
            }

            // When a response contains a response body:
            // The response MUST also contain another HTTP header X-Order-Media-Type containing the major version
            // and format information. For example, X-Order-Media-Type: order.v1; format=json.
            openApiResponse.Value.Headers[ApiConstants.OrderMediaTypeHeaderName] = new OpenApiHeader
            {
                Description = "The major API version and format information.",
                Example = new OpenApiString(ApiConstants.DefaultOrderMediaTypeHeaderValue),
                Schema = new OpenApiSchema { Type = "string" }
            };
        }
    }
}
