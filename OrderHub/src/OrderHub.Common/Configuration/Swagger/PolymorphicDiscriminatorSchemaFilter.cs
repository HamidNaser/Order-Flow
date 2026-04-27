using System.Reflection;
using System.Text.Json.Serialization;
using OrderHub.Contracts;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrderHub.Common.Configuration.Swagger;

public class PolymorphicDiscriminatorSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var polymorphicDiscriminatorAttribute = context.Type.GetCustomAttribute<PolymorphicDiscriminatorAttribute>();
        if (polymorphicDiscriminatorAttribute == null) return;

        var jsonPolymorphicAttribute = context.Type.GetCustomAttribute<JsonPolymorphicAttribute>();
        if (jsonPolymorphicAttribute == null) return;

        var jsonDerivedTypeAttributes = context.Type.GetCustomAttributes<JsonDerivedTypeAttribute>();

        schema.Discriminator = new OpenApiDiscriminator
        {
            PropertyName = jsonPolymorphicAttribute.TypeDiscriminatorPropertyName ?? "type",
            Mapping = new Dictionary<string, string>()
        };

        var discriminatorPropertyName = jsonPolymorphicAttribute.TypeDiscriminatorPropertyName ?? "type";

        if (!schema.Properties.ContainsKey(discriminatorPropertyName))
        {
            schema.Required.Add(discriminatorPropertyName);
            schema.Properties[discriminatorPropertyName] = new OpenApiSchema
            {
                Type = "string",
                Description = "The type of order.",
                Example = new OpenApiString("STANDARD, DIGITAL, DIRECT")
            };
        }

        foreach (var derivedTypeAttr in jsonDerivedTypeAttributes)
        {
            var discriminatorValue = derivedTypeAttr.TypeDiscriminator?.ToString() ?? derivedTypeAttr.DerivedType.Name;
            var schemaReference = $"#/components/schemas/{derivedTypeAttr.DerivedType.Name}";
            schema.Discriminator.Mapping[discriminatorValue] = schemaReference;
        }
    }
}
