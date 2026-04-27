using Asp.Versioning.ApiExplorer;

namespace Microsoft.AspNetCore.Builder;

public static partial class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwagger(this IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger();

        app.UseSwaggerUI(
            options =>
            {
                options.EnableTryItOutByDefault();

                foreach (var description in provider.ApiVersionDescriptions.OrderByDescending(d => d.ApiVersion.MajorVersion))
                {
                    options.SwaggerEndpoint($"{description.GroupName}/swagger.json", description.GroupName);
                }
            }
        );

        return app;
    }
}
