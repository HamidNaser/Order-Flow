using Asp.Versioning.ApiExplorer;
using OrderHub.Api.Configuration.App;
using OrderHub.Common.Configuration;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("{ApplicationName} Application Starting..", Defs.ApplicationName);

    var builder = WebApplication.CreateBuilder(args);

    var brandedAppSettingsFile = BrandedConfigurationFileNames.GetAppSettingsFileName(builder.Environment.EnvironmentName);
    if (brandedAppSettingsFile is not null)
    {
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, brandedAppSettingsFile), optional: false, reloadOnChange: true);
    }

    builder
        .AddServiceDefaults(Defs.ApplicationName)
        .AddApiDefaults(Defs.ConfigurationSectionName, Defs.ApplicationName, Defs.OpenApiDescription);

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
    Log.Fatal(ex, "{ApplicationName} Application Terminated Unexpectedly", Defs.ApplicationName);
}
finally
{
    Log.Information("{ApplicationName} Application Shutting Down..", Defs.ApplicationName);
    Log.CloseAndFlush();
}
