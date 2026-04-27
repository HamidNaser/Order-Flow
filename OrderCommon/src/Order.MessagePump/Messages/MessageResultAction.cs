namespace Order.MessagePump.Messages
{
    public enum MessageResultAction
    {
        Complete,
        Retry,
        Poison
    }
}
