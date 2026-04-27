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
    builder.Services.AddSingleton<OrderQueryService>();
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

builder.Services.AddSingleton<MessageStorageService>();
builder.Services.AddSingleton<QueueReplayService>();
builder.Services.AddSingleton<S3OperationsService>();

var app = builder.Build();

// Enable Swagger for all environments (internal tooling only)
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
