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

public class SendMessageRequest
{
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string>? MessageAttributes { get; set; }
    public string? MessageGroupId { get; set; }
}

public class UploadS3ObjectRequest
{
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/json";
}

// ── Trace / Polling ───────────────────────────────────────────────

public class WaitForS3ObjectRequest
{
    public string BucketName { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int PollIntervalMs { get; set; } = 500;
}

public class WaitForQueueMessageRequest
{
    public string QueueName { get; set; } = string.Empty;
    public string? BodyContains { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int PollIntervalMs { get; set; } = 500;
}

public class WaitForMongoDocumentRequest
{
    public string StoreId { get; set; } = string.Empty;
    public string? ProviderOrderId { get; set; }
    public string? CustomerId { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int PollIntervalMs { get; set; } = 500;
}
