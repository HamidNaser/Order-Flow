using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon.S3.Transfer;
using OrderHub.Contracts.Utility;

namespace OrderHub.Common.Services;

public class S3Service(
    IAmazonS3 s3Client,
    ITransferUtility transferUtility,
    JsonSerializerOptions jsonSerializerOptions
) : IS3Service
{
    public async Task<List<string>> GetObjectKeysByPrefix(string bucketName, string prefix)
    {
        var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
        });

        var result = response?.S3Objects?.Select(o => o.Key).ToList() ?? [];

        return result;
    }

    public async Task PutObjectAsync(S3PutObjectRequest request)
    {
        if (request.UseMultipartUpload)
        {
            var s3PutMultipartObjectRequest = new S3PutMultipartObjectRequest
            {
                BucketName = request.BucketName,
                Key = request.Key,
                BinaryContent = Encoding.UTF8.GetBytes(request.ContentBody)
            };

            await PutMultipartObjectAsync(s3PutMultipartObjectRequest);

            return;
        }

        var putRequest = new PutObjectRequest
        {
            BucketName = request.BucketName,
            Key = request.Key,
            ContentBody = request.ContentBody
        };

        await s3Client.PutObjectAsync(putRequest);
    }

    public async Task PutMultipartObjectAsync(S3PutMultipartObjectRequest request)
    {
        using var memoryStream = new MemoryStream(request.BinaryContent);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = memoryStream,
            BucketName = request.BucketName,
            Key = request.Key,
            AutoCloseStream = true
        };

        await transferUtility.UploadAsync(uploadRequest);
    }

    public async Task<S3GetObjectResponse> GetObjectAsync(string bucketName, string key)
    {
        try
        {
            var response = await s3Client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                }
            );

            using var reader = new StreamReader(response.ResponseStream);
            var content = await reader.ReadToEndAsync();

            return new S3GetObjectResponse { Content = content };
        }
        catch (AmazonS3Exception ex)
        {
            return new S3GetObjectResponse
            {
                ErrorType = ClassifyS3Error(ex),
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            return new S3GetObjectResponse
            {
                ErrorType = S3ErrorType.UNEXPECTED,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<S3DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key)
    {
        try
        {
            await s3Client.DeleteObjectAsync(
                new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                }
            );

            return new S3DeleteObjectResponse();
        }
        catch (AmazonS3Exception ex)
        {
            return new S3DeleteObjectResponse
            {
                ErrorType = ClassifyS3Error(ex),
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            return new S3DeleteObjectResponse
            {
                ErrorType = S3ErrorType.UNEXPECTED,
                ErrorMessage = ex.Message
            };
        }
    }

    private static S3ErrorType ClassifyS3Error(AmazonS3Exception ex)
    {
        return ex.StatusCode == HttpStatusCode.NotFound
            || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase)
            ? S3ErrorType.NOT_FOUND
            : S3ErrorType.UNEXPECTED;
    }
    public async Task PutObjectAsync<T>(S3PutObjectRequest<T> request)
    {
        var contentBody = JsonSerializer.Serialize(request.Payload, jsonSerializerOptions);

        var stringRequest = new S3PutObjectRequest
        {
            Key = request.Key,
            BucketName = request.BucketName,
            ContentBody = contentBody,
            UseMultipartUpload = request.UseMultipartUpload
        };

        await PutObjectAsync(stringRequest);
    }

    public async Task<S3GetObjectResponse<T>> GetObjectAsync<T>(string bucketName, string key)
    {
        var stringResult = await GetObjectAsync(bucketName, key);

        if (stringResult.ErrorType != S3ErrorType.NONE)
        {
            return new S3GetObjectResponse<T>
            {
                ErrorType = stringResult.ErrorType,
                ErrorMessage = stringResult.ErrorMessage,
            };
        }

        try
        {
            var parsedContent = JsonSerializer.Deserialize<T>(stringResult.Content, jsonSerializerOptions);

            return new S3GetObjectResponse<T>
            {
                Content = parsedContent,
            };
        }
        catch (JsonException ex)
        {
            return new S3GetObjectResponse<T>
            {
                Content = default,
                ErrorType = S3ErrorType.PARSING_ERROR,
                ErrorMessage = ex.Message,
            };
        }
    }

    public async Task BulkDeleteObjectsAsync(string bucketName, List<string> keys)
    {
        var deleteRequest = new DeleteObjectsRequest
        {
            BucketName = bucketName,
            Objects = keys.Select(k => new KeyVersion { Key = k }).ToList()
        };

        await s3Client.DeleteObjectsAsync(deleteRequest);
    }
}
