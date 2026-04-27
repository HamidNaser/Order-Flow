using System.ComponentModel.DataAnnotations;
using OrderGateway.Common.Models.Events;

namespace OrderGateway.Common.Models.Api;

/// <summary>
/// Request model for the order event handler integration test endpoint.
/// Wraps an OrderEvent for processing through the handler pipeline.
/// </summary>
public class OrderEventRequest
{
    /// <summary>
    /// The order event to process through the handler pipeline.
    /// </summary>
    [Required]
    public required OrderEvent Event { get; set; }
    
    /// <summary>
    /// The number of times this message has been received from the queue.
    /// Used to simulate retry logic and poison message handling in integration tests.
    /// Defaults to 1 (first attempt).
    /// </summary>
    public int ApproximateReceiveCount { get; set; } = 1;
}
