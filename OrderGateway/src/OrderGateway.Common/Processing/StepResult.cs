using Order.MessagePump.Messages;

namespace OrderGateway.Common.Processing;

public readonly struct StepResult
{
    public bool ShouldContinue { get; }
    public MessageResult? Result { get; }

    private StepResult(bool shouldContinue, MessageResult? result)
    {
        ShouldContinue = shouldContinue;
        Result = result;
    }

    public static StepResult Continue() => new(true, null);
    public static StepResult Complete(MessageResult result) => new(false, result);
    public static StepResult Complete() => Complete(MessageResult.Complete());
    public static StepResult Complete(string details) => Complete(MessageResult.Complete(details));
}
