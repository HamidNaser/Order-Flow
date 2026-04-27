using Amazon.SQS.Model;

namespace DlqReplayTool.Models;

public class MessageReplayResult
{
    public string MessageId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> MessageAttributes { get; set; } = new();
    public string? MessageGroupId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
