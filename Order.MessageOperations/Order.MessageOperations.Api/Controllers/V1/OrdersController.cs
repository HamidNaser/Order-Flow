using Order.MessageOperations.Api.Models;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Controllers.V1;

/// <summary>
/// Read-only endpoints for querying the OrderHub orders database.
/// Provides operational visibility into stored order records for AI-assisted debugging.
/// </summary>
[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderQueryService _queryService;

    public OrdersController(OrderQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// Get a single order by StoreId and OrderId.
    /// </summary>
    [HttpGet("{storeId}/{orderId}")]
    public async Task<IActionResult> GetById(string storeId, string orderId, CancellationToken ct)
    {
        var record = await _queryService.GetByIdAsync(storeId, orderId, ct);

        if (record == null)
            return NotFound(new { message = $"Order '{orderId}' not found in Store '{storeId}'." });

        return Ok(record);
    }

    /// <summary>
    /// List orders for a specific customer within a store.
    /// </summary>
    [HttpGet("{storeId}/customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(
        string storeId, string customerId,
        [FromQuery] int limit = 50, [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var records = await _queryService.GetByCustomerAsync(storeId, customerId, limit, offset, ct);
        var count = await _queryService.CountByCustomerAsync(storeId, customerId, ct);

        return Ok(new
        {
            storeId,
            customerId,
            totalCount = count,
            returned = records.Count,
            limit,
            offset,
            orders = records
        });
    }

    /// <summary>
    /// Get a count of orders for a customer.
    /// </summary>
    [HttpGet("{storeId}/customer/{customerId}/count")]
    public async Task<IActionResult> CountByCustomer(string storeId, string customerId, CancellationToken ct)
    {
        var count = await _queryService.CountByCustomerAsync(storeId, customerId, ct);
        return Ok(new { storeId, customerId, count });
    }

    /// <summary>
    /// Search orders with flexible filter criteria.
    /// </summary>
    [HttpGet("{storeId}/search")]
    public async Task<IActionResult> Search(
        string storeId,
        [FromQuery] string? customerId = null,
        [FromQuery] string? channelType = null,
        [FromQuery] string? fulfillmentStatus = null,
        [FromQuery] string? orderFlow = null,
        [FromQuery] string? providerId = null,
        [FromQuery] string? providerName = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var searchParams = new OrderSearchParams
        {
            CustomerId = customerId,
            ChannelType = channelType,
            FulfillmentStatus = fulfillmentStatus,
            OrderFlow = orderFlow,
            ProviderId = providerId,
            ProviderName = providerName,
            FromDate = fromDate,
            ToDate = toDate,
            Limit = limit,
            Offset = offset
        };

        var records = await _queryService.SearchAsync(storeId, searchParams, ct);

        return Ok(new
        {
            storeId,
            filters = searchParams,
            returned = records.Count,
            orders = records
        });
    }

    /// <summary>
    /// Get a summary of orders for a CoOrg - counts by channel type, fulfillment status, direction.
    /// </summary>
    [HttpGet("{storeId}/summary")]
    public async Task<IActionResult> GetSummary(string storeId, CancellationToken ct)
    {
        var summary = await _queryService.GetSummaryAsync(storeId, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Find a order by provider details (provider name + provider order ID).
    /// </summary>
    [HttpGet("{storeId}/provider/{providerName}/{providerOrderId}")]
    public async Task<IActionResult> FindByProvider(
        string storeId, string providerName, string providerOrderId,
        [FromQuery] string? channelType = null,
        CancellationToken ct = default)
    {
        var record = await _queryService.FindByProviderAsync(storeId, providerOrderId, providerName, channelType, ct);

        if (record == null)
            return NotFound(new { message = $"Order not found for provider '{providerName}' with ID '{providerOrderId}' in Store '{storeId}'." });

        return Ok(record);
    }

    /// <summary>
    /// List the most recent orders for a CoOrg (regardless of consumer).
    /// </summary>
    [HttpGet("{storeId}/recent")]
    public async Task<IActionResult> GetRecent(
        string storeId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var records = await _queryService.GetRecentAsync(storeId, limit, ct);

        return Ok(new
        {
            storeId,
            returned = records.Count,
            orders = records
        });
    }
}
