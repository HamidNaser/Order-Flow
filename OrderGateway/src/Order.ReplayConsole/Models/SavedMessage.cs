namespace DlqReplayTool.Models;

public class SavedMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, MessageAttributeValue> MessageAttributes { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();
    public string? MessageGroupId { get; set; }
    public string ReceiptHandle { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    public string SourceDlq { get; set; } = string.Empty;
}

public class MessageAttributeValue
{
    public string? StringValue { get; set; }
    public string? DataType { get; set; }
}

public class MessageBatch
{
    public string BatchId { get; set; } = string.Empty;
    public string QueueType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string SourceDlq { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public List<string> MessageIds { get; set; } = new();
}
