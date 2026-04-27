using System.Net;
using Asp.Versioning;
using OrderHub.Common.Configuration.Auth;
using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;
using OrderHub.Contracts.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderHub.Api.Controllers.V0;

[ApiController]
[ApiVersion("0.0")]
[Route("api/s3")]
[Authorize(Policy = ApiKeyAuthenticationDefaults.AuthorizationPolicy)]
[Consumes("application/vnd.order.v0+json")]
[Produces("application/vnd.order.v0+json")]
public class S3Controller(IS3Service s3Service, S3Config s3Config) : ControllerBase
{
    [HttpPost("put-object", Name = $"S3{nameof(PutObject)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PutObject([FromBody] S3PutObjectRequest request)
    {
        await s3Service.PutObjectAsync(request);
        return NoContent();
    }

    [HttpPost("put-multipart-object", Name = $"S3{nameof(PutMultipartObject)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PutMultipartObject([FromBody] S3PutMultipartObjectRequest request)
    {
        var s3PutMultipartObjectRequest = new S3PutMultipartObjectRequest
        {
            BucketName = request.BucketName,
            Key = request.Key,
            BinaryContent = request.BinaryContent
        };

        await s3Service.PutMultipartObjectAsync(s3PutMultipartObjectRequest);
        return NoContent();
    }

    [HttpGet("get-object", Name = $"S3{nameof(GetObject)}")]
    [ProducesResponseType(typeof(S3GetObjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(S3GetObjectResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(S3GetObjectResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetObject([FromQuery] string bucketName, [FromQuery] string key)
    {
        var response = await s3Service.GetObjectAsync(bucketName, key);

        if (response.ErrorType == S3ErrorType.NONE)
        {
            return Ok(new S3GetObjectResponse { Content = response.Content });
        }

        return response.ErrorType == S3ErrorType.NOT_FOUND ? NotFound(response) : BadRequest(response);
    }

    [HttpDelete("delete-object", Name = $"S3{nameof(DeleteObject)}")]
    [ProducesResponseType(typeof(S3DeleteObjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(S3DeleteObjectResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(S3DeleteObjectResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteObject([FromQuery] string bucketName, [FromQuery] string key)
    {
        var response = await s3Service.DeleteObjectAsync(bucketName, key);

        if (response.ErrorType == S3ErrorType.NONE)
        {
            return Ok(new S3DeleteObjectResponse());
        }

        return response.ErrorType == S3ErrorType.NOT_FOUND ? NotFound(response) : BadRequest(response);
    }

    [HttpGet("order-object/{objectKey}", Name = $"S3{nameof(GetOrderObject)}")]
    [ProducesResponseType(typeof(OrderRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(S3GetObjectResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderObject(string objectKey)
    {
        var parsedKey = WebUtility.UrlDecode(objectKey);
        var response = await s3Service.GetObjectAsync<OrderRequest>(s3Config.OrderBucketName, parsedKey);

        if (response.ErrorType == S3ErrorType.NOT_FOUND)
        {
            return NotFound();
        }

        if (response.ErrorType != S3ErrorType.NONE)
        {
            return BadRequest(response);
        }

        return Ok(response.Content);
    }

    [HttpDelete("bulk-delete-order-object", Name = $"S3{nameof(BulkDeleteOrderObjects)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkDeleteOrderObjects([FromBody] List<string> keys)
    {
        await s3Service.BulkDeleteObjectsAsync(s3Config.OrderBucketName, keys);

        return NoContent();
    }
}
