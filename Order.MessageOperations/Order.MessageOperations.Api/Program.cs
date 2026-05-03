using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// MongoDB / DocumentDB connection for orders read-only queries
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB");
if (!string.IsNullOrWhiteSpace(mongoConnectionString))
{
    builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));
    builder.Services.AddSingleton<IOrderQueryService, OrderQueryService>();
}

builder.Services.Configure<MessageOperationsOptions>(options =>
{
    builder.Configuration.GetSection("MessageOperations").Bind(options);

    if (!string.IsNullOrWhiteSpace(options.MessageStoragePath) && !Path.IsPathRooted(options.MessageStoragePath))
    {
        var baseDirectory = AppContext.BaseDirectory;
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

builder.Services.AddSingleton<IMessageStorageService, MessageStorageService>();
builder.Services.AddSingleton<IQueueReplayService, QueueReplayService>();
builder.Services.AddSingleton<IS3OperationsService, S3OperationsService>();
builder.Services.AddSingleton<ITraceService, TraceService>();
builder.Services.AddSingleton<ITestDataService, TestDataService>();

var app = builder.Build();

// Enable Swagger for all environments (internal tooling only)
app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var response = new { error = "An unexpected error occurred." };
        await context.Response.WriteAsJsonAsync(response);
    });
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
