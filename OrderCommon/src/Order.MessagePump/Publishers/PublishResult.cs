namespace Order.MessagePump.Publishers
{
    public class PublishResult
    {
        public string Id { get; set; } = string.Empty;

        public bool Success { get; set; }

        public string? Message { get; set; }
    }
}
