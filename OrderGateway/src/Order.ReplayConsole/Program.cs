using DlqReplayTool.Models;
using DlqReplayTool.Services;
using DlqReplayTool.UI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Setup dependency injection
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add configuration with absolute path resolution
services.Configure<DlqReplayConfig>(options =>
{
    configuration.GetSection("DlqReplay").Bind(options);
    
    // Convert MessageStoragePath to absolute path if it's relative
    if (!string.IsNullOrEmpty(options.MessageStoragePath) && !Path.IsPathRooted(options.MessageStoragePath))
    {
        // Use the solution directory (go up from bin/Debug/net8.0 or current directory)
        var baseDirectory = AppContext.BaseDirectory; // This is bin/Debug/net8.0 when running from VS
        var projectDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", ".."));
        options.MessageStoragePath = Path.GetFullPath(Path.Combine(projectDirectory, options.MessageStoragePath));
    }

    if (string.IsNullOrWhiteSpace(options.S3CachePath))
    {
        options.S3CachePath = Path.Combine(options.MessageStoragePath, "s3-cache");
    }

    if (!Path.IsPathRooted(options.S3CachePath))
    {
        var baseDirectory = AppContext.BaseDirectory;
        var projectDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", ".."));
        options.S3CachePath = Path.GetFullPath(Path.Combine(projectDirectory, options.S3CachePath));
    }
});

// Add services
services.AddSingleton<MessageStorageService>();
services.AddSingleton<DlqReplayService>();
services.AddSingleton<S3SyncService>();
services.AddSingleton<InteractiveMenu>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

try
{
    // Setup cancellation token for graceful shutdown
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        Console.WriteLine("\n\n⚠️  Cancellation requested. Stopping gracefully...");
        e.Cancel = true;
        cts.Cancel();
    };

    // Run interactive menu
    var menu = serviceProvider.GetRequiredService<InteractiveMenu>();
    await menu.RunAsync(cts.Token);

    Environment.Exit(0);
}
catch (Exception ex)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Fatal error during execution");
    Console.WriteLine($"\n❌ Fatal error: {ex.Message}");
    Environment.Exit(1);
}
finally
{
    var replayService = serviceProvider.GetService<DlqReplayService>();
    replayService?.Dispose();
}
