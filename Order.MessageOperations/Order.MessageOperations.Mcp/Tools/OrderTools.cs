using System.ComponentModel;
using System.Text.Json;
using Order.MessageOperations.Mcp.Client;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Tools;

/// <summary>
/// MCP tools for querying the OrderHub orders database.
/// Provides read-only access to stored order records for AI-assisted debugging.
/// </summary>
[McpServerToolType]
public class OrderTools
{
    private readonly MessageOperationsClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OrderTools(MessageOperationsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Get a single order record by StoreId and OrderId.
    /// </summary>
    [McpServerTool]
    [Description("Get a single order record by StoreId and OrderId. Returns full details including channel-specific fields, provider info, and delivery status.")]
    public async Task<string> GetOrder(
        [Description("The Common Org ID (StoreId) that owns the order")] string storeId,
        [Description("The MongoDB ObjectId of the order record")] string orderId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeId) || string.IsNullOrWhiteSpace(orderId))
            return "Error: Both storeId and orderId are required.";

        var record = await _client.GetOrderByIdAsync(storeId, orderId, ct);

        if (record == null)
            return $"Order '{orderId}' not found in Store '{storeId}'.";

        return JsonSerializer.Serialize(record, JsonOptions);
    }

    /// <summary>
    /// List orders for a specific customer within a store.
    /// </summary>
    [McpServerTool]
    [Description("List orders for a specific customer within a store. Returns paginated results sorted by date descending. Includes total count for pagination.")]
    public async Task<string> GetCustomerOrders(
        [Description("The Store ID")] string storeId,
        [Description("The customer ID to look up")] string customerId,
        [Description("Max number of records to return (default: 50, max: 200)")] int limit = 50,
        [Description("Number of records to skip for pagination (default: 0)")] int offset = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeId) || string.IsNullOrWhiteSpace(customerId))
            return "Error: Both storeId and customerId are required.";

        var result = await _client.GetCustomerOrdersAsync(storeId, customerId, limit, offset, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Search orders with flexible filter criteria.
    /// </summary>
    [McpServerTool]
    [Description("Search orders with flexible filters: customerId, channelType (STANDARD/DIGITAL), fulfillmentStatus (IN_PROGRESS/SUCCESS/FAILED), orderFlow (INCOMING/OUTGOING), providerName, providerId, date range. Returns paginated results.")]
    public async Task<string> SearchOrders(
        [Description("The Store ID")] string storeId,
        [Description("Filter by customer ID (optional)")] string? customerId = null,
        [Description("Filter by channel type: STANDARD or DIGITAL (optional)")] string? channelType = null,
        [Description("Filter by fulfillment status: IN_PROGRESS, SUCCESS, or FAILED (optional)")] string? fulfillmentStatus = null,
        [Description("Filter by order flow: INCOMING or OUTGOING (optional)")] string? orderFlow = null,
        [Description("Filter by provider name (optional)")] string? providerName = null,
        [Description("Filter by provider order ID (optional)")] string? providerId = null,
        [Description("Filter by start date (UTC, ISO 8601 format, optional)")] string? fromDate = null,
        [Description("Filter by end date (UTC, ISO 8601 format, optional)")] string? toDate = null,
        [Description("Max number of records to return (default: 50, max: 200)")] int limit = 50,
        [Description("Number of records to skip for pagination (default: 0)")] int offset = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeId))
            return "Error: storeId is required.";

        DateTime? parsedFromDate = null;
        DateTime? parsedToDate = null;

        if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var fd))
            parsedFromDate = fd;
        if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var td))
            parsedToDate = td;

        var result = await _client.SearchOrdersAsync(
            storeId, customerId, channelType, fulfillmentStatus, orderFlow,
            providerName, providerId, parsedFromDate, parsedToDate, limit, offset, ct);

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Get a summary of orders for a store.
    /// </summary>
    [McpServerTool]
    [Description("Get a summary of order counts for a store. Shows total count and breakdowns by channel type (STANDARD/DIGITAL), fulfillment status, and order flow.")]
    public async Task<string> GetOrderSummary(
        [Description("The Store ID")] string storeId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeId))
            return "Error: storeId is required.";

        var summary = await _client.GetOrderSummaryAsync(storeId, ct);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    /// <summary>
    /// Find an order by provider details.
    /// </summary>
    [McpServerTool]
    [Description("Find an order by provider details - useful when you have a provider's order ID and want to find the corresponding record.")]
    public async Task<string> FindByProvider(
        [Description("The Store ID")] string storeId,
        [Description("The provider name (e.g., 'ShipStation', 'FedEx')")] string providerName,
        [Description("The provider's order ID")] string providerOrderId,
        [Description("Optional channel type filter: STANDARD or DIGITAL")] string? channelType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeId) || string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(providerOrderId))
            return "Error: storeId, providerName, and providerOrderId are all required.";

        var record = await _client.FindByProviderAsync(storeId, providerName, providerOrderId, channelType, ct);

        if (record == null)
            return $"Order not found for provider '{providerName}' with ID '{providerOrderId}' in Store '{storeId}'.";

        return JsonSerializer.Serialize(record, JsonOptions);
    }

    /// <summary>
    /// List the most recent orders for a CoOrg.
    /// </summary>
    [McpServerTool]
    [Description("List the most recent orders for a CoOrg regardless of consumer. Useful for seeing the latest activity.")]
    public async Task<string> GetRecentOrders(
        [Description("The Common Org ID (StoreId)")] string storeId,
        [Description("Max number of records to return (default: 20, max: 200)")] int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeId))
            return "Error: storeId is required.";

        var result = await _client.GetRecentOrdersAsync(storeId, limit, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
