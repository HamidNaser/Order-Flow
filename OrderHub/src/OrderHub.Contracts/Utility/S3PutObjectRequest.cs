namespace OrderHub.Contracts.Utility;

public class S3PutObjectRequest<T> : S3PutObjectRequestBase
{
    public required T Payload { get; init; }
    public bool UseMultipartUpload { get; init; }
}

public class S3PutObjectRequest : S3PutObjectRequestBase
{
    public required string ContentBody { get; init; }
    public bool UseMultipartUpload { get; init; }
}
