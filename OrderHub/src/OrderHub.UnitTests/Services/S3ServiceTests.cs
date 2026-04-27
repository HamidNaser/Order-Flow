using System.Net;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OrderHub.Common.Services;
using OrderHub.Contracts.Utility;
using Xunit;

namespace OrderHub.UnitTests.Services;

public class S3ServiceTests
{
    private readonly IAmazonS3 _s3Client = Substitute.For<IAmazonS3>();
    private readonly ITransferUtility _transferUtility = Substitute.For<ITransferUtility>();
    private readonly S3Service _sut;

    public S3ServiceTests()
    {
        _sut = new S3Service(_s3Client, _transferUtility, new JsonSerializerOptions());
    }

    // ──────────────────────────────────────────────
    // GetObjectAsync — error classification
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetObjectAsync_WhenKeyNotFound_ReturnsNotFound()
    {
        // Arrange
        var ex = new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound };
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.GetObjectAsync("bucket", "missing-key");

        // Assert
        Assert.Equal(S3ErrorType.NOT_FOUND, result.ErrorType);
        Assert.Contains("Not Found", result.ErrorMessage);
    }

    [Fact]
    public async Task GetObjectAsync_WhenNoSuchKeyErrorCode_ReturnsNotFound()
    {
        // Arrange
        var ex = new AmazonS3Exception("The specified key does not exist.")
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "NoSuchKey"
        };
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.GetObjectAsync("bucket", "missing-key");

        // Assert
        Assert.Equal(S3ErrorType.NOT_FOUND, result.ErrorType);
    }

    [Fact]
    public async Task GetObjectAsync_WhenNoSuchBucket_ReturnsNotFound()
    {
        // Arrange
        var ex = new AmazonS3Exception("The specified bucket does not exist.")
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "NoSuchBucket"
        };
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.GetObjectAsync("bad-bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.NOT_FOUND, result.ErrorType);
    }

    [Fact]
    public async Task GetObjectAsync_WhenAccessDenied_ReturnsUnexpected()
    {
        // Arrange
        var ex = new AmazonS3Exception("Access Denied") { StatusCode = HttpStatusCode.Forbidden };
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.GetObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.UNEXPECTED, result.ErrorType);
        Assert.Contains("Access Denied", result.ErrorMessage);
    }

    [Fact]
    public async Task GetObjectAsync_WhenServiceUnavailable_ReturnsUnexpected()
    {
        // Arrange
        var ex = new AmazonS3Exception("Service Unavailable") { StatusCode = HttpStatusCode.ServiceUnavailable };
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.GetObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.UNEXPECTED, result.ErrorType);
    }

    [Fact]
    public async Task GetObjectAsync_WhenInternalServerError_ReturnsUnexpected()
    {
        // Arrange
        var ex = new AmazonS3Exception("Internal Server Error") { StatusCode = HttpStatusCode.InternalServerError };
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.GetObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.UNEXPECTED, result.ErrorType);
    }

    [Fact]
    public async Task GetObjectAsync_WhenGenericException_ReturnsUnexpected()
    {
        // Arrange
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("something broke"));

        // Act
        var result = await _sut.GetObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.UNEXPECTED, result.ErrorType);
        Assert.Contains("something broke", result.ErrorMessage);
    }

    // ──────────────────────────────────────────────
    // DeleteObjectAsync — error classification
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteObjectAsync_WhenKeyNotFound_ReturnsNotFound()
    {
        // Arrange
        var ex = new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound };
        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.DeleteObjectAsync("bucket", "missing-key");

        // Assert
        Assert.Equal(S3ErrorType.NOT_FOUND, result.ErrorType);
    }

    [Fact]
    public async Task DeleteObjectAsync_WhenAccessDenied_ReturnsUnexpected()
    {
        // Arrange
        var ex = new AmazonS3Exception("Access Denied") { StatusCode = HttpStatusCode.Forbidden };
        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.DeleteObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.UNEXPECTED, result.ErrorType);
    }

    [Fact]
    public async Task DeleteObjectAsync_WhenServiceUnavailable_ReturnsUnexpected()
    {
        // Arrange
        var ex = new AmazonS3Exception("Service Unavailable") { StatusCode = HttpStatusCode.ServiceUnavailable };
        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

        // Act
        var result = await _sut.DeleteObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.UNEXPECTED, result.ErrorType);
    }

    [Fact]
    public async Task DeleteObjectAsync_WhenGenericException_ReturnsUnexpected()
    {
        // Arrange
        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("something broke"));

        // Act
        var result = await _sut.DeleteObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.UNEXPECTED, result.ErrorType);
    }

    // ──────────────────────────────────────────────
    // DeleteObjectAsync — success
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteObjectAsync_WhenSuccessful_ReturnsNone()
    {
        // Act
        var result = await _sut.DeleteObjectAsync("bucket", "key");

        // Assert
        Assert.Equal(S3ErrorType.NONE, result.ErrorType);
    }
}
