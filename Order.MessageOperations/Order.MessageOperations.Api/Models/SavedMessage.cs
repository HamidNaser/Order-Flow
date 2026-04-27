namespace Order.MessageOperations.Api.Models;

public class SavedMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, MessageAttributeValueModel> MessageAttributes { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();
    public string? MessageGroupId { get; set; }
    public string ReceiptHandle { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    public string SourceDlq { get; set; } = string.Empty;
}

public class MessageAttributeValueModel
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

public class S3BucketInfo
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}

public class S3ObjectInfo
{
    public string Key { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string? ETag { get; set; }
    public string? StorageClass { get; set; }
}

public class S3ObjectMetadataInfo
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public string? ContentType { get; set; }
    public string? ETag { get; set; }
    public DateTime LastModified { get; set; }
}

public class S3ObjectContentResult
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long ContentLength { get; set; }
    public string Content { get; set; } = string.Empty;
}
