namespace OrderHub.Contracts.Utility;

public class S3GetObjectResponse
{
    public string Content { get; init; } = string.Empty;
    public S3ErrorType ErrorType { get; init; } = S3ErrorType.NONE;
    public string ErrorMessage { get; init; } = string.Empty;
}

public class S3GetObjectResponse<T>
{
    public T? Content { get; init; }
    public S3ErrorType ErrorType { get; init; } = S3ErrorType.NONE;
    public string ErrorMessage { get; init; } = string.Empty;
}

public class S3DeleteObjectResponse : S3GetObjectResponse;
