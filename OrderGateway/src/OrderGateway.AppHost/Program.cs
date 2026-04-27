var builder = DistributedApplication.CreateBuilder(args);
var orderGatewayApi = builder.AddProject<Projects.OrderGateway_Api>("order-gateway-api")
    .WithEnvironment("DOTNET_ENVIRONMENT", "localstack")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "localstack")
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithExternalHttpEndpoints();

// Order Worker
var orderWorker = builder.AddProject<Projects.OrderGateway_OrderWorker>("order-gateway-order-worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", "localstack")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "localstack")
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithEnvironment("QueueClientOptions__IncomingOrders__QueueName", "order-gateway-incoming");

builder.Build().Run();
