using Order.MessageOperations.Mcp.Client;
using Order.MessageOperations.Mcp.Configuration;
using Order.MessageOperations.Mcp.Prompts;
using Order.MessageOperations.Mcp.Resources;
using Order.MessageOperations.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging - reduce noise for MCP (stderr should be minimal)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Warning);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Get API base URL from environment or use default
var apiBaseUrl = Environment.GetEnvironmentVariable("MESSAGEOPS_API_URL") ?? "http://localhost:5100";

// Bind configuration
builder.Services.Configure<McpServerOptions>(options =>
{
    options.ApiBaseUrl = apiBaseUrl;
});

// Register typed HTTP client for calling the MessageOperations API
builder.Services.AddHttpClient<MessageOperationsClient>((serviceProvider, client) =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add MCP server with tools from this assembly
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "order-message-ops",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools<QueueTools>()
    .WithTools<BatchTools>()
    .WithTools<ReplayTools>()
    .WithTools<S3Tools>()
    .WithTools<OrderTools>()
    .WithTools<HealthTools>()
    .WithTools<TraceTools>()
    .WithTools<TestDataTools>()
    .WithPrompts<OrderPrompts>()
    .WithResources<OrderResources>();

// Build and run
var host = builder.Build();

// Log startup info (to stderr so it doesn't interfere with MCP)
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogWarning("Starting Order MessageOperations MCP Server");
logger.LogWarning("API Base URL: {ApiBaseUrl}", apiBaseUrl);

await host.RunAsync();
