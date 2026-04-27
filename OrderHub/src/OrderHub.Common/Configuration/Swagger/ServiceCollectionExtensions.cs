using Asp.Versioning.ApiExplorer;
using OrderHub.Common.Configuration.Auth;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using OrderHub.Common.Configuration.Swagger;
using OrderHub.Contracts;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureSwagger(
        this IServiceCollection services,
        string applicationName,
        string openApiDescription,
        string oauth2Authority,
        BridgeOAuthPolicy[] oauth2Policies
    )
    {
        services.AddSwaggerGen(options =>

        {
            options.CustomSchemaIds(x => DefaultSchemaIdSelector(x));
            options.OperationFilter<OrderApiVersionSupportFilter>();
            options.SchemaFilter<PolymorphicDiscriminatorSchemaFilter>();
            options.SchemaFilter<HiddenPropertySchemaFilter>();
            options.DocumentFilter<ApiKeyDocumentFilter>();
            // options.UseOneOfForPolymorphism();

            options.UseAllOfForInheritance();

            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(ChannelTypeConstants).Namespace}.xml"));
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{applicationName}.xml"), true);
        });

        services
            .AddOptions<SwaggerGenOptions>()
            .Configure<IApiVersionDescriptionProvider>(
                (options, merchant) =>
                {
                    foreach (var description in merchant.ApiVersionDescriptions)
                    {
                        options.SwaggerDoc(
                            description.GroupName,
                            new OpenApiInfo
                            {
                                Title = applicationName,
                                Version = description.ApiVersion.ToString(),
                                Description = openApiDescription
                            }
                        );
                    }

                    options.DescribeAllParametersInCamelCase();

                    // Add API Key definition globally - will be filtered out for non-V0 by ApiKeyDocumentFilter
                    options.AddSecurityDefinition(
                        ApiKeyAuthenticationDefaults.AuthorizationPolicy,
                        new OpenApiSecurityScheme
                        {
                            Name = ApiKeyAuthenticationDefaults.HeaderName,
                            Description = "Enter API Key (V0 endpoints only)",
                            In = ParameterLocation.Header,
                            Type = SecuritySchemeType.ApiKey
                        }
                    );

                    options.AddSecurityDefinition(
                        BridgeOAuthSettings.RawTokenAuthorizationPolicy,
                        new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.Http,
                            Name = BridgeOAuthSettings.RawTokenAuthorizationPolicy,
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = BridgeOAuthSettings.RawTokenAuthorizationPolicy
                            },
                            Scheme = "bearer",
                            BearerFormat = "JWT",
                            In = ParameterLocation.Header,
                            Description = "Enter JWT Bearer Token (Please use this exclusively for authenticating using OAuth <Bridge> within SwaggerUI)."
                        }
                    );

                    // Add security definitions for each OAuth policy
                    foreach (var policy in oauth2Policies)
                    {
                        options.AddSecurityDefinition(
                            policy.Name,
                            new OpenApiSecurityScheme
                            {
                                Type = SecuritySchemeType.OAuth2,
                                Name = policy.Name,
                                Description = "Please use RawToken scheme when authenticating using OAuth <Bridge> within SwaggerUI.",
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = policy.Name
                                },
                                Flows = new OpenApiOAuthFlows
                                {
                                    ClientCredentials = new OpenApiOAuthFlow
                                    {
                                        Scopes = policy.Scopes
                                            .ToDictionary(keySelector: x => x, elementSelector: n => ""),
                                        TokenUrl = new Uri(oauth2Authority + "/v1/token")
                                    },
                                },
                            }
                        );
                    }

                    options.OperationFilter<SecurityRequirementsOperationFilter>();
                }
            );

        return services;
    }

    // https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/752#issuecomment-467817189
    // using type.fullname would not pass cip validation when uploading yaml to the storefront
    private static string DefaultSchemaIdSelector(Type modelType)
    {
        if (!modelType.IsConstructedGenericType) return modelType.Name;

        var prefix = modelType.GetGenericArguments()
            .Select(genericArg => DefaultSchemaIdSelector(genericArg))
            .Aggregate((previous, current) => previous + current);

        return modelType.Name.Split('`').First() + "Of" + prefix;
    }
}
