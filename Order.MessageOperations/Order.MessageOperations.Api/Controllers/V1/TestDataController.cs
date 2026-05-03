using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/test-data")]
public class TestDataController : ControllerBase
{
    private readonly ITestDataService _testDataService;

    public TestDataController(ITestDataService testDataService)
    {
        _testDataService = testDataService;
    }

    /// <summary>
    /// Generate test order payloads for injection into the processing pipeline.
    /// </summary>
    /// <param name="priority">Order priority: "standard" or "express" (default: standard)</param>
    /// <param name="channelType">Channel type: "STANDARD" or "DIGITAL" (default: STANDARD). Only affects ingest format.</param>
    /// <param name="count">Number of orders to generate (default: 1, max: 50)</param>
    /// <param name="storeId">Override the store ID (default: random)</param>
    /// <param name="format">"gateway" (base64 OrderEvent for SQS) or "ingest" (JSON for HTTP POST to IngestAPI)</param>
    [HttpPost("generate-orders")]
    public IActionResult GenerateOrders(
        [FromQuery] string priority = "standard",
        [FromQuery] string channelType = "STANDARD",
        [FromQuery] int count = 1,
        [FromQuery] string? storeId = null,
        [FromQuery] string format = "gateway")
    {
        if (count < 1 || count > 50)
            return BadRequest(new ErrorResponse("Count must be between 1 and 50"));

        if (!priority.Equals("standard", StringComparison.OrdinalIgnoreCase) &&
            !priority.Equals("express", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ErrorResponse("Priority must be 'standard' or 'express'"));

        if (!format.Equals("gateway", StringComparison.OrdinalIgnoreCase) &&
            !format.Equals("ingest", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ErrorResponse("Format must be 'gateway' or 'ingest'"));

        var orders = _testDataService.GenerateOrders(priority, channelType, count, storeId, format);

        return Ok(new GenerateOrdersResponse(
            Count: orders.Count,
            Priority: priority.ToUpperInvariant(),
            ChannelType: channelType.ToUpperInvariant(),
            Format: format.ToLowerInvariant(),
            TargetQueue: orders.FirstOrDefault()?.TargetQueue ?? "",
            Orders: orders));
    }
}
