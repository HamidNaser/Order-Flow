using System.Collections.Generic;

namespace Order.MessagePump.Publishers
{
    public class PublishEntry
    {
        public string Id { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public Dictionary<string, string>? Attributes { get; set; }
    }
}
