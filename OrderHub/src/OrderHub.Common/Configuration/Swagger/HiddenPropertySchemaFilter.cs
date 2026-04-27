using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrderHub.Common.Configuration.Swagger;

/// <summary>
/// Schema filter that removes specific properties from request schemas in Swagger.
/// This ensures properties are not included in API request examples while still
/// appearing in response schemas.
/// </summary>
public class HiddenPropertySchemaFilter : ISchemaFilter
{
    private static readonly Dictionary<string, HashSet<string>> HiddenProperties = new()
    {
    };

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties == null || context.Type == null)
            return;

        var typeName = context.Type.Name;

        if (HiddenProperties.TryGetValue(typeName, out var hiddenProps))
        {
            foreach (var propName in hiddenProps)
            {
                var key = schema.Properties.Keys.FirstOrDefault(k => 
                    k.Equals(propName, StringComparison.OrdinalIgnoreCase));

                if (key != null)
                {
                    // Remove the property entirely from the schema
                    schema.Properties.Remove(key);
                }
            }
        }
    }
}
