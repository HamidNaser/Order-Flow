using OrderHub.Common.Configuration;
using OrderHub.Common.Configuration.Swagger;
using OrderHub.IngestStandard.Api.Configuration.App;

namespace OrderHub.IngestStandard.Api.Configuration.Swagger;

/// <remarks>
/// https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/configure-and-customize-cli.md#use-the-cli-tool-with-a-custom-host-configuration
/// </remarks>
public class SwaggerHostFactory
{
    public static IHost CreateHost() =>
        Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var brandedAppSettingsFile = BrandedConfigurationFileNames.GetAppSettingsFileName(context.HostingEnvironment.EnvironmentName);
                if (brandedAppSettingsFile is not null)
                {
                    config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, brandedAppSettingsFile), optional: false, reloadOnChange: true);
                }
            })
            .ConfigureWebHostDefaults(webBuilder => webBuilder
                .UseStartup(_ => new SwaggerHostStartup(Defs.ApplicationName, Defs.OpenApiDescription)))
            .Build();
}

