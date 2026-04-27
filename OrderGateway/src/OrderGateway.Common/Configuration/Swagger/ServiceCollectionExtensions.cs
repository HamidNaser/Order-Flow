using Asp.Versioning.ApiExplorer;
using OrderGateway.Common.Configuration;
using OrderGateway.Common.Configuration.Auth;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Runtime.Serialization;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureSwagger(this IServiceCollection services, ApplicationName applicationName)
    {
        services.AddSwaggerGen(options =>
        {
            options.SchemaFilter<MakeExceptionSchemaNullableFilter>();
            options.UseAllOfToExtendReferenceSchemas();
        });

        services
            .AddOptions<SwaggerGenOptions>()
            .Configure<IApiVersionDescriptionProvider>(
                (options, provider) =>
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerDoc(
                            description.GroupName,
                            new OpenApiInfo
                            {
                                Title = applicationName.ToString(),
                                Version = description.GroupName
                            }
                        );
                    }

                    options.CustomSchemaIds(type => type.GetCustomAttribute<DataContractAttribute>()?.Name ?? type.FullName);

                    options.DescribeAllParametersInCamelCase();

                    options.AddSecurityDefinition(
                        ApiKeyAuthenticationDefaults.AuthorizationPolicy,
                        new OpenApiSecurityScheme
                        {
                            Name = ApiKeyAuthenticationDefaults.HeaderName,
                            Description = "Enter API Key",
                            In = ParameterLocation.Header,
                            Type = SecuritySchemeType.ApiKey
                        }
                    );

                    options.AddSecurityRequirement(
                        new OpenApiSecurityRequirement
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Id = ApiKeyAuthenticationDefaults.AuthorizationPolicy,
                                        Type = ReferenceType.SecurityScheme
                                    }
                                },
                                []
                            }
                        }
                    );

                    foreach (var xmlDocumentationPath in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly))
                    {
                        options.IncludeXmlComments(xmlDocumentationPath, includeControllerXmlComments: true);
                    }

                    // TimeSpans need custom mapping, because the default mapping is to a "datespan" format, which is not supported by NSwag.
                    options.MapType<TimeSpan>(() => new OpenApiSchema { Type = "string", Format = "time-span" });
                    options.MapType<TimeSpan?>(() => new OpenApiSchema { Type = "string", Format = "time-span", Nullable = true });
                }
            );
        return services;
    }
}

//the schema filter approach does not set nullable: true for $ref properties in OpenAPI 3.0, due to a limitation
//in the OpenAPI specification and how Swashbuckle generates schemas. When a property is a reference ($ref),
//OpenAPI 3.0 does not allow the nullable flag to be set directly on the property; it must be set on the referenced
//schema itself, which is not what you want for all usages of System.Exception.
//We still went ahead with this approach as this is a testing controller.

public class MakeExceptionSchemaNullableFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(Exception))
        {
            schema.Nullable = true;
        }
    }
}
