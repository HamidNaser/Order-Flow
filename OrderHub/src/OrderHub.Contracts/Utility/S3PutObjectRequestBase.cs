namespace OrderHub.Contracts.Utility;

public class S3PutObjectRequestBase
{
    public required string BucketName { get; init; }
    public required string Key { get; init; }
}
