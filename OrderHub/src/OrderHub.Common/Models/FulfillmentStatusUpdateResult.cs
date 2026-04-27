namespace OrderHub.Common.Models;

public class FulfillmentStatusUpdateResult
{
    public FulfillmentStatusUpdateAction Action { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }
    public string? OrderId { get; init; }
    public List<string>? MediaIds { get; init; }
    public ChannelOrder? Order { get; init; }

    public static FulfillmentStatusUpdateResult Success(
        string? message = null,
        string? orderId = null,
        List<string>? mediaIds = null,
        ChannelOrder? order = null) =>
        new()
        {
            Action = FulfillmentStatusUpdateAction.SUCCESS,
            Message = message,
            OrderId = orderId,
            MediaIds = mediaIds,
            Order = order
        };

    public static FulfillmentStatusUpdateResult NotFound(string? message = null) =>
        new() { Action = FulfillmentStatusUpdateAction.NOT_FOUND, Message = message };

    public static FulfillmentStatusUpdateResult AlreadyProcessed(string? message = null) =>
        new() { Action = FulfillmentStatusUpdateAction.ALREADY_PROCESSED, Message = message };

    public static FulfillmentStatusUpdateResult UpdateFailed(string? message = null, Exception? exception = null) =>
        new() { Action = FulfillmentStatusUpdateAction.UPDATE_FAILED, Message = message, Exception = exception };
}
