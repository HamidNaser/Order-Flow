using Asp.Versioning.ApiExplorer;
using OrderGateway.Common.Configuration;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

const ApplicationName applicationName = ApplicationName.OrderGatewayApi;

try
{
    Log.Information("{ApplicationName} Application Starting..", applicationName);

    var builder = WebApplication.CreateBuilder(args);

    var brandedAppSettingsFile = BrandedConfigurationFileNames.GetAppSettingsFileName(builder.Environment.EnvironmentName);
    if (brandedAppSettingsFile is not null)
    {
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, brandedAppSettingsFile), optional: false, reloadOnChange: true);
    }

    builder.Configuration
        .AddDecryptedInMemoryCollection(builder.Configuration)
        .AddSplunkAppNameOverrideInMemoryCollection(applicationName, builder.Configuration);

    builder.Services
        .ConfigureLogging(builder.Configuration)
        .ConfigureApi()
        .ConfigureAuth(builder.Configuration, applicationName)
        .ConfigureHealthChecks()
        .ConfigureVersioning()
#if DEBUG
        .ConfigureSwagger(applicationName)
#endif
        .ConfigureCache(builder.Configuration)
        .ConfigureServices(builder.Configuration)
        .AddCorrelationIdSupport(builder.Configuration)
        .ConfigureHttpClients(builder.Configuration)
        .ConfigureManagers(builder.Configuration)
        .ConfigureLaunchDarkly(builder.Configuration)
        .ConfigureQueues(builder.Configuration)
        .ConfigureHandlers(builder.Configuration)
        ;

    var app = builder.Build();

    using var scope = app.Services.CreateScope();

    app
        .UseApi()
#if DEBUG
        .UseSwagger(scope.ServiceProvider.GetRequiredService<IApiVersionDescriptionProvider>())
#endif
        .UseHealthChecks();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "{ApplicationName} Application Terminated Unexpectedly", applicationName);
}
finally
{
    Log.Information("{ApplicationName} Application Shutting Down..", applicationName);
    Log.CloseAndFlush();
}
