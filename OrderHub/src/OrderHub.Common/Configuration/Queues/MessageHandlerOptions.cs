namespace OrderHub.Common.Configuration.Queues;

public class MessageHandlerOptions
{
    public int MaxMessageRetries { get; set; } = 3;
}
