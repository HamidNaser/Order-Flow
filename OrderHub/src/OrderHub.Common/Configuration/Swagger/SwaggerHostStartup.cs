using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace OrderHub.Common.Configuration.Swagger;

public class SwaggerHostStartup(string applicationName, string openApiDescription)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .ConfigureJsonSerializerOptions()
            .ConfigureApi()
            .ConfigureVersioning()
            .ConfigureSwagger(
                applicationName,
                openApiDescription,
                "https://placeholder.order.com",
                []
            );
    }

    public void Configure(IApplicationBuilder app, IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    {
        app
            .UseApi()
            .UseSwagger(apiVersionDescriptionProvider);
    }
}
