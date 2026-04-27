var builder = DistributedApplication.CreateBuilder(args);

const string expressQueueName = "order-hub-express-order";
const string standardQueueName = "order-hub-standard-order";

// Order API (primary read/write API)
var orderApi = builder.AddProject<Projects.OrderHub_Api>("order-api")
    .WithEnvironment("DOTNET_ENVIRONMENT", "localstack")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "localstack")
    .WithEnvironment("Aws__Connection__ServiceUrl", "http://localhost:4566")
    .WithEnvironment("Aws__Connection__Region", "us-east-1")
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithExternalHttpEndpoints();

// Ingest Express API and Worker
var ingestExpressApi = builder.AddProject<Projects.OrderHub_IngestExpress_Api>("ingest-express-api")
    .WithEnvironment("DOTNET_ENVIRONMENT", "localstack")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "localstack")
    .WithEnvironment("Aws__Connection__ServiceUrl", "http://localhost:4566")
    .WithEnvironment("Aws__Connection__Region", "us-east-1")
    .WithEnvironment("QueueClientOptions__EXPRESS__QueueName", expressQueueName)
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithExternalHttpEndpoints();

var ingestExpressWorker = builder.AddProject<Projects.OrderHub_IngestExpress_Worker>("ingest-express-worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", "localstack")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "localstack")
    .WithEnvironment("Aws__Connection__ServiceUrl", "http://localhost:4566")
    .WithEnvironment("Aws__Connection__Region", "us-east-1")
    .WithEnvironment("QueueClientOptions__EXPRESS__QueueName", expressQueueName)
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithReference(ingestExpressApi);

// Ingest Standard API and Worker
var ingestStandardApi = builder.AddProject<Projects.OrderHub_IngestStandard_Api>("ingest-standard-api")
    .WithEnvironment("DOTNET_ENVIRONMENT", "localstack")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "localstack")
    .WithEnvironment("Aws__Connection__ServiceUrl", "http://localhost:4566")
    .WithEnvironment("Aws__Connection__Region", "us-east-1")
    .WithEnvironment("QueueClientOptions__STANDARD__QueueName", standardQueueName)
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithExternalHttpEndpoints();

var ingestStandardWorker = builder.AddProject<Projects.OrderHub_IngestStandard_Worker>("ingest-standard-worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", "localstack")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "localstack")
    .WithEnvironment("Aws__Connection__ServiceUrl", "http://localhost:4566")
    .WithEnvironment("Aws__Connection__Region", "us-east-1")
    .WithEnvironment("QueueClientOptions__STANDARD__QueueName", standardQueueName)
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithReference(ingestStandardApi);

builder.Build().Run();
