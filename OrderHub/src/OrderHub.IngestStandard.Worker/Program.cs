using OrderHub.Common.Configuration;
using OrderHub.Common.Configuration.Queues;
using OrderHub.IngestStandard.Worker.Configuration.App;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("{ApplicationName} Application Starting..", Defs.ApplicationName);

    var builder = Host.CreateApplicationBuilder(args);

    var brandedAppSettingsFile = BrandedConfigurationFileNames.GetAppSettingsFileName(builder.Environment.EnvironmentName);
    if (brandedAppSettingsFile is not null)
    {
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, brandedAppSettingsFile), optional: false, reloadOnChange: true);
    }

    builder
        .AddServiceDefaults(Defs.ApplicationName)
        .AddWorkerDefaults([Queues.STANDARD]);

    var host = builder.Build();

    host.Run();
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

