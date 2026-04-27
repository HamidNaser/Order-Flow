using OrderGateway.Common.Configuration;
using OrderGateway.Common.Configuration.Queues;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

const ApplicationName applicationName = ApplicationName.OrderWorker;

try
{
    Log.Information("{ApplicationName} Application Starting..", applicationName);

    var builder = Host.CreateApplicationBuilder(args);

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
        .ConfigureCache(builder.Configuration)
        .ConfigureServices(builder.Configuration)
        .AddCorrelationIdSupport(builder.Configuration)
        .ConfigureHttpClients(builder.Configuration)
        .ConfigureManagers(builder.Configuration)
        .ConfigureLaunchDarkly(builder.Configuration)
        .ConfigureHandlers(builder.Configuration)
        .ConfigureQueues(builder.Configuration)
        .StartQueueMessageWorker(SupportedQueues.IncomingOrders);

    var host = builder.Build();

    host.Run();
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
