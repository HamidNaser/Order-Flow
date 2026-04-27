namespace Order.MessageOperations.Api.Models.Requests;

public class DownloadMessagesRequest
{
    public string QueueKey { get; set; } = string.Empty;
    public string? AwsQueueName { get; set; }
    public int? MaxMessages { get; set; }
    public string? MessageId { get; set; }
}

public class ReplayBatchRequest
{
    public string QueueType { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string? LocalStackQueueName { get; set; }
}

public class DownloadAndReplayRequest
{
    public string QueueKey { get; set; } = string.Empty;
    public int? MaxMessages { get; set; }
    public string? MessageId { get; set; }
}

public class SyncS3FromBatchRequest
{
    public string QueueType { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public bool UseAwsFallback { get; set; }
}
