using Order.MessagePump.Messages;

namespace OrderGateway.Common.Models
{
    public static class MessageResultExtensions
    {
        public static HandlerResultDto ToDto(this MessageResult result)
        {
            if (result is ProcessingResult processingResult)
            {
                return new HandlerResultDto
                {
                    Action = processingResult.Action,
                    Details = processingResult.Details,
                    Backoff = processingResult.Backoff,
                    ExceptionMessage = processingResult.Exception?.Message ?? string.Empty,
                    IsSuccess = processingResult.IsSuccess,
                    StepContext = processingResult.StepContext
                };
            }

            return new HandlerResultDto
            {
                Action = result.Action,
                Details = result.Details,
                Backoff = result.Backoff,
                ExceptionMessage = result.Exception?.Message ?? string.Empty,
                IsSuccess = result.Action == MessageResultAction.Complete && string.IsNullOrWhiteSpace(result.Details)
            };
        }
    }
}
