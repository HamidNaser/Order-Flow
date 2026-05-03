using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for generating test order data and injecting it into the pipeline.
/// </summary>
[McpServerToolType]
public class TestDataTools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TestDataTools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Generate realistic test orders and return them ready to send to queues.
    /// </summary>
    [McpServerTool]
    [Description(@"Generate realistic test order payloads for the order processing pipeline. Returns ready-to-send message bodies.

Use 'gateway' format (default) for sending to the order-gateway-incoming queue — this generates base64-encoded OrderEvent messages that the OrderGateway will transform and route.

Use 'ingest' format for direct HTTP POST to the IngestStandard/IngestExpress APIs.

Priority controls routing:
  - 'standard' → orders marked as batch classification, routed to standard processing path
  - 'express' → orders marked as manual, routed to express processing path

After generating, use SendTestMessage to send each order's body to its targetQueue.")]
    public async Task<string> GenerateTestOrders(
        [Description("Number of orders to generate (1-50, default: 1)")] int count = 1,
        [Description("Priority: 'standard' or 'express' (default: standard)")] string priority = "standard",
        [Description("Output format: 'gateway' (base64 for SQS queue) or 'ingest' (JSON for HTTP API)")] string format = "gateway",
        [Description("Channel type: 'STANDARD' (shipment) or 'DIGITAL' (default: STANDARD)")] string channelType = "STANDARD",
        [Description("Store ID override (default: 10001 — enabled in local feature flags)")] string? storeId = null,
        CancellationToken ct = default)
    {
        var result = await _client.GenerateTestOrdersAsync(priority, channelType, count, storeId, format, ct);

        if (result == null)
            return "Error: Unable to reach the MessageOperations API. Is it running?";

        var output = new
        {
            generated = result.Count,
            priority = result.Priority,
            format = result.Format,
            targetQueue = result.TargetQueue,
            orders = result.Orders.Select(o => new
            {
                o.Index,
                o.OrderReferenceId,
                o.StoreId,
                o.Priority,
                o.TargetQueue,
                o.Description,
                bodyLength = o.Body.Length,
                // Include the actual body so the AI can send it
                body = o.Body
            }),
            nextStep = $"Use SendTestMessage to send each order's 'body' to queue '{result.TargetQueue}'. " +
                       $"Then use WaitForQueueMessage or GetAllQueueDepths to verify delivery."
        };

        return JsonSerializer.Serialize(output, JsonOptions);
    }

    /// <summary>
    /// Generate test orders AND send them all to the appropriate queue in one step.
    /// </summary>
    [McpServerTool]
    [Description(@"Generate test orders and immediately send them all to the target LocalStack queue. Returns send results for each order.

This is a convenience tool that combines GenerateTestOrders + SendTestMessage in one call.
After sending, use GetAllQueueDepths or WaitForQueueMessage to trace message flow.

Examples:
  - 'Send 5 standard orders' → count=5, priority='standard'
  - 'Send 3 express orders to store 10001' → count=3, priority='express', storeId='10001'")]
    public async Task<string> GenerateAndSendOrders(
        [Description("Number of orders to generate and send (1-50, default: 1)")] int count = 1,
        [Description("Priority: 'standard' or 'express' (default: standard)")] string priority = "standard",
        [Description("Channel type: 'STANDARD' (shipment) or 'DIGITAL' (default: STANDARD)")] string channelType = "STANDARD",
        [Description("Store ID override (default: 10001 — enabled in local feature flags)")] string? storeId = null,
        CancellationToken ct = default)
    {
        // Step 1: Generate orders (always gateway format for queue injection)
        var generated = await _client.GenerateTestOrdersAsync(priority, channelType, count, storeId, "gateway", ct);

        if (generated == null)
            return "Error: Unable to reach the MessageOperations API. Is it running?";

        // Step 2: Send each order to its target queue
        var results = new List<object>();
        var successCount = 0;

        foreach (var order in generated.Orders)
        {
            try
            {
                var sendResult = await _client.SendMessageAsync(order.TargetQueue, order.Body, ct: ct);
                successCount++;
                results.Add(new
                {
                    order.Index,
                    order.OrderReferenceId,
                    order.StoreId,
                    order.Description,
                    sent = true,
                    messageId = sendResult?.MessageId ?? "unknown",
                    queue = order.TargetQueue
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    order.Index,
                    order.OrderReferenceId,
                    order.StoreId,
                    order.Description,
                    sent = false,
                    error = ex.Message,
                    queue = order.TargetQueue
                });
            }
        }

        var output = new
        {
            totalGenerated = generated.Count,
            totalSent = successCount,
            totalFailed = generated.Count - successCount,
            priority = generated.Priority,
            targetQueue = generated.TargetQueue,
            orders = results,
            nextSteps = new[]
            {
                $"Use GetAllQueueDepths to verify {successCount} messages are on '{generated.TargetQueue}'",
                "Use WaitForQueueMessage to trace individual order processing",
                "Use WaitForS3Object to verify S3 persistence (if workers are running)",
                "Use WaitForMongoDocument to verify database persistence (if workers are running)"
            }
        };

        return JsonSerializer.Serialize(output, JsonOptions);
    }
}
