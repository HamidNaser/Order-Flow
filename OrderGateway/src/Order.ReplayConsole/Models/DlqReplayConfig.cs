namespace DlqReplayTool.Models;

public class DlqReplayConfig
{
    public string AwsRegion { get; set; } = "us-east-1";
    public string AwsAccountId { get; set; } = string.Empty;
    public string Environment { get; set; } = "qa"; // qa, staging, production
    public string LocalStackEndpoint { get; set; } = "http://localhost:4566";
    public string LocalStackSqsEndpoint { get; set; } = string.Empty;
    public string LocalStackS3Endpoint { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 10;
    public string MessageStoragePath { get; set; } = "downloaded-messages";
    public string S3CachePath { get; set; } = string.Empty;
    public Dictionary<string, QueueMapping> Queues { get; set; } = new();
}

public class QueueMapping
{
    public string DisplayName { get; set; } = string.Empty;
    public string LocalStackQueueName { get; set; } = string.Empty;
    public string AwsDlqName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
