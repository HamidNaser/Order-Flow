using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Resources;

/// <summary>
/// MCP resources that provide automatic context to the AI about the order processing system.
/// These are read-only snapshots the AI sees when a conversation starts — no explicit tool calls needed.
/// </summary>
[McpServerResourceType]
public class OrderResources
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OrderResources(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Full system topology: all configured queues, S3 buckets, infrastructure endpoints, and MongoDB connection.
    /// This gives the AI a complete map of the order processing system without needing to call any tools.
    /// </summary>
    [McpServerResource(
        UriTemplate = "order-ops://topology",
        Name = "system-topology",
        MimeType = "application/json")]
    public async Task<ReadResourceResult> GetSystemTopology(CancellationToken ct = default)
    {
        // Gather queue configuration
        var queues = await _client.ListConfiguredQueuesAsync(ct);

        // Gather S3 buckets (best-effort — may fail if LocalStack is down)
        List<S3BucketDto>? buckets = null;
        try
        {
            buckets = await _client.ListS3BucketsAsync(ct: ct);
        }
        catch
        {
            // LocalStack may not be running — that's OK for topology
        }

        // Gather health status (best-effort)
        LocalStackHealthDto? health = null;
        try
        {
            health = await _client.CheckLocalStackHealthAsync(ct);
        }
        catch
        {
            // API may not be fully ready
        }

        var topology = new
        {
            system = "Order Processing Platform",
            description = "Distributed order processing pipeline: OrderGateway → S3 → OrderHub → MongoDB",
            infrastructure = new
            {
                localStack = new
                {
                    endpoint = "http://localhost:4566",
                    sqsEndpoint = "http://sqs.us-east-1.localhost.localstack.cloud:4566",
                    s3Endpoint = "http://localhost:4566",
                    healthy = health?.Healthy,
                    services = health != null ? new
                    {
                        sqs = new { healthy = health.Sqs.Healthy, detail = health.Sqs.Detail },
                        s3 = new { healthy = health.S3.Healthy, detail = health.S3.Detail }
                    } : null
                },
                mongodb = new
                {
                    connectionString = "mongodb://127.0.0.1:27018/?directConnection=true",
                    description = "Order persistence — stores processed orders from Hub workers"
                },
                redis = new
                {
                    orderHub = "localhost:6379",
                    orderGateway = "localhost:6380"
                },
                keycloak = new
                {
                    endpoint = "http://localhost:8081",
                    oidcDiscovery = "http://localhost:8081/realms/ordergateway-local/.well-known/openid-configuration"
                }
            },
            queues = new
            {
                count = queues.Count,
                configured = queues.Select(q => new
                {
                    key = q.QueueKey,
                    displayName = q.DisplayName,
                    localStackQueue = q.LocalStackQueueName,
                    dlq = q.AwsDlqName,
                    enabled = q.Enabled,
                    system = q.QueueKey.StartsWith("Ingest") || q.QueueKey == "FulfillmentStatus"
                        ? "OrderHub"
                        : "OrderGateway"
                })
            },
            s3 = new
            {
                buckets = buckets?.Select(b => new { name = b.Name, created = b.CreationDate }),
                notificationRules = new[]
                {
                    new { bucket = "localstack-us-east-1-orders", prefix = "STANDARD/", targetQueue = "order-hub-standard-order" },
                    new { bucket = "localstack-us-east-1-orders", prefix = "EXPRESS/", targetQueue = "order-hub-express-order" }
                }
            },
            pipeline = new
            {
                description = "Message flow: IncomingOrders → Gateway Worker → S3 → S3 Notification → Hub Queue → Hub Worker → MongoDB",
                hops = new[]
                {
                    new { step = 1, from = "External Publisher", to = "order-gateway-incoming", action = "Enqueue order message" },
                    new { step = 2, from = "order-gateway-incoming", to = "Gateway OrderWorker", action = "Dequeue and process order" },
                    new { step = 3, from = "Gateway OrderWorker", to = "S3 (localstack-us-east-1-orders)", action = "Write order JSON to S3 with STANDARD/ or EXPRESS/ prefix" },
                    new { step = 4, from = "S3 Notification", to = "order-hub-standard-order OR order-hub-express-order", action = "S3 event triggers SQS notification to Hub ingest queue" },
                    new { step = 5, from = "Hub Ingest Queue", to = "Hub Worker (Standard or Express)", action = "Dequeue and process order" },
                    new { step = 6, from = "Hub Worker", to = "MongoDB", action = "Persist order document" }
                }
            },
            appHosts = new
            {
                orderHub = new
                {
                    project = "OrderHub/src/OrderHub.AppHost/OrderHub.AppHost.csproj",
                    services = new[] { "OrderHub.Api", "OrderHub.IngestStandard.Api", "OrderHub.IngestExpress.Api", "OrderHub.IngestStandard.Worker", "OrderHub.IngestExpress.Worker" }
                },
                orderGateway = new
                {
                    project = "OrderGateway/src/OrderGateway.AppHost/OrderGateway.AppHost.csproj",
                    services = new[] { "OrderGateway.Api", "OrderGateway.OrderWorker" }
                }
            }
        };

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "order-ops://topology",
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(topology, JsonOptions)
                }
            ]
        };
    }

    /// <summary>
    /// Live queue depth snapshot — current message count for every configured queue.
    /// Shows the AI whether messages are accumulating, stuck, or flowing normally.
    /// </summary>
    [McpServerResource(
        UriTemplate = "order-ops://queue-health",
        Name = "queue-health",
        MimeType = "application/json")]
    public async Task<ReadResourceResult> GetQueueHealth(CancellationToken ct = default)
    {
        AllQueueDepthsResultDto? depths = null;
        string? error = null;

        try
        {
            depths = await _client.GetAllQueueDepthsAsync(ct);
        }
        catch (Exception ex)
        {
            error = $"Unable to retrieve queue depths: {ex.Message}";
        }

        object queueHealth;

        if (depths != null)
        {
            queueHealth = new
            {
                status = depths.TotalMessages == 0 ? "idle" : "active",
                totalMessages = depths.TotalMessages,
                queues = depths.Queues.Select(q => new
                {
                    key = q.QueueKey,
                    queue = q.QueueName,
                    messages = q.ApproximateMessageCount,
                    inFlight = q.ApproximateNotVisible,
                    isDlq = q.QueueName.Contains("deadletter"),
                    alert = q.QueueName.Contains("deadletter") && q.ApproximateMessageCount > 0
                        ? "DLQ has messages — investigate failed processing"
                        : null
                }),
                summary = BuildQueueSummary(depths)
            };
        }
        else
        {
            queueHealth = new
            {
                status = "unavailable",
                error,
                suggestion = "LocalStack or the MessageOperations API may not be running. Try the setup-localstack prompt."
            };
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "order-ops://queue-health",
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(queueHealth, JsonOptions)
                }
            ]
        };
    }

    /// <summary>
    /// Recent orders from MongoDB — the last 10 orders across all configured stores.
    /// Gives the AI immediate context about what orders have been processed recently.
    /// </summary>
    [McpServerResource(
        UriTemplate = "order-ops://recent-orders",
        Name = "recent-orders",
        MimeType = "application/json")]
    public async Task<ReadResourceResult> GetRecentOrders(CancellationToken ct = default)
    {
        object recentOrders;

        try
        {
            // Try to get recent orders from the default store
            var orders = await _client.GetRecentOrdersAsync("ALL", limit: 10, ct: ct);

            recentOrders = new
            {
                status = "ok",
                description = "Last 10 orders across all stores from MongoDB",
                orders
            };
        }
        catch (Exception ex)
        {
            recentOrders = new
            {
                status = "unavailable",
                error = $"Unable to retrieve recent orders: {ex.Message}",
                suggestion = "MongoDB may not be running, or no orders have been processed yet."
            };
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "order-ops://recent-orders",
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(recentOrders, JsonOptions)
                }
            ]
        };
    }

    private static string BuildQueueSummary(AllQueueDepthsResultDto depths)
    {
        var mainQueues = depths.Queues.Where(q => !q.QueueName.Contains("deadletter")).ToList();
        var dlqQueues = depths.Queues.Where(q => q.QueueName.Contains("deadletter")).ToList();
        var dlqWithMessages = dlqQueues.Where(q => q.ApproximateMessageCount > 0).ToList();

        var parts = new List<string>
        {
            $"{depths.TotalMessages} total messages across {depths.Queues.Count} queues"
        };

        if (dlqWithMessages.Count > 0)
        {
            var dlqTotal = dlqWithMessages.Sum(q => q.ApproximateMessageCount);
            parts.Add($"⚠ {dlqTotal} messages in {dlqWithMessages.Count} DLQ(s) — investigate failures");
        }
        else
        {
            parts.Add("No DLQ messages — all processing healthy");
        }

        var activeMain = mainQueues.Where(q => q.ApproximateMessageCount > 0).ToList();
        if (activeMain.Count > 0)
        {
            parts.Add($"{activeMain.Count} queue(s) have pending messages: {string.Join(", ", activeMain.Select(q => $"{q.QueueName}={q.ApproximateMessageCount}"))}");
        }

        return string.Join(". ", parts) + ".";
    }
}
