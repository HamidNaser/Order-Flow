using Order.MessagePump.Messages;
using OrderGateway.Common.Processing.Abstractions;

namespace OrderGateway.Common.Models
{
    public class HandlerResultDto
    {
        public MessageResultAction Action { get; set; }
        public string? Details { get; set; }
        public TimeSpan? Backoff { get; set; }
        public string? ExceptionMessage { get; set; }
        public bool IsSuccess { get; set; }
        public StepContext? StepContext { get; set; }
    }
}
