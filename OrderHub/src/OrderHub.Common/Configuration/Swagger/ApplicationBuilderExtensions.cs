using Asp.Versioning.ApiExplorer;

namespace Microsoft.AspNetCore.Builder;

public static partial class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwagger(this IApplicationBuilder app, IApiVersionDescriptionProvider merchant)
    {
        app.UseSwagger();

        app.UseSwaggerUI(
            options =>
            {
                foreach (var description in merchant.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint(
                        $"{description.GroupName}/swagger.json",
                        $"Version {description.ApiVersion}"
                    );
                }
            }
        );

        return app;
    }
}
