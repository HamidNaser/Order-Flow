namespace OrderGateway.Common.Models.Events;

public interface IOrderEvent : IEvent
{
    UserContactType UserContactType { get; }
    string Contact { get; }
    int CustomerId { get; }
    int UserId { get; }
    bool IsStandardPriority { get; }
}
