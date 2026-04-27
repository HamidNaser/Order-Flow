namespace OrderHub.Contracts.Utility;

public class S3PutMultipartObjectRequest : S3PutObjectRequestBase
{
    public required byte[] BinaryContent { get; init; }
}
