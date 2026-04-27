using OrderHub.Api.Configuration.App;
using OrderHub.Common.Configuration.Swagger;

namespace OrderHub.Api.Configuration.Swagger;

/// <remarks>
/// https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/configure-and-customize-cli.md#use-the-cli-tool-with-a-custom-host-configuration
/// </remarks>
public class SwaggerHostFactory
{
    public static IHost CreateHost() =>
        Host
            .CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder => webBuilder
                .UseStartup(_ => new SwaggerHostStartup(Defs.ApplicationName, Defs.OpenApiDescription)))
            .Build();
}
