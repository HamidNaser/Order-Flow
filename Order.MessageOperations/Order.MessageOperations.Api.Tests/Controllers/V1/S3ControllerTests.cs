using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models;
using Order.MessageOperations.Api.Models.Requests;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class S3ControllerTests
{
    private readonly IS3OperationsService _s3Service;
    private readonly IMessageStorageService _storageService;
    private readonly S3Controller _sut;

    public S3ControllerTests()
    {
        _s3Service = Substitute.For<IS3OperationsService>();
        _storageService = Substitute.For<IMessageStorageService>();
        _sut = new S3Controller(_s3Service, _storageService);
    }

    // --- ListBuckets ---

    [Fact]
    public async Task ListBuckets_LocalStack_CallsWithTrue()
    {
        // Arrange
        var buckets = new List<S3BucketInfo>
        {
            new() { Name = "bucket-1", CreationDate = DateTime.UtcNow }
        };
        _s3Service.ListBucketsAsync(true, Arg.Any<CancellationToken>()).Returns(buckets);

        // Act
        var result = await _sut.ListBuckets("localstack", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(buckets, ok.Value);
    }

    [Fact]
    public async Task ListBuckets_Aws_CallsWithFalse()
    {
        // Arrange
        _s3Service.ListBucketsAsync(false, Arg.Any<CancellationToken>())
            .Returns(new List<S3BucketInfo>());

        // Act
        var result = await _sut.ListBuckets("aws", CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        await _s3Service.Received(1).ListBucketsAsync(false, Arg.Any<CancellationToken>());
    }

    // --- ListObjects ---

    [Fact]
    public async Task ListObjects_ReturnsObjectList()
    {
        // Arrange
        var objects = new List<S3ObjectInfo>
        {
            new() { Key = "file.json", Size = 1024 }
        };
        _s3Service.ListObjectsAsync("my-bucket", "prefix/", 100, true, Arg.Any<CancellationToken>())
            .Returns(objects);

        // Act
        var result = await _sut.ListObjects("my-bucket", "prefix/", 100, "localstack", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(objects, ok.Value);
    }

    // --- GetObjectMetadata ---

    [Fact]
    public async Task GetObjectMetadata_EmptyKey_ReturnsBadRequest()
    {
        // Act
        var result = await _sut.GetObjectMetadata("bucket", "", "localstack", CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetObjectMetadata_ValidKey_ReturnsMetadata()
    {
        // Arrange
        var metadata = new S3ObjectMetadataInfo
        {
            Bucket = "bucket",
            Key = "file.json",
            ContentLength = 2048
        };
        _s3Service.GetObjectMetadataAsync("bucket", "file.json", true, Arg.Any<CancellationToken>())
            .Returns(metadata);

        // Act
        var result = await _sut.GetObjectMetadata("bucket", "file.json", "localstack", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(metadata, ok.Value);
    }

    // --- GetObjectContent ---

    [Fact]
    public async Task GetObjectContent_EmptyKey_ReturnsBadRequest()
    {
        // Act
        var result = await _sut.GetObjectContent("bucket", "", cancellationToken: CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetObjectContent_ValidKey_ReturnsContent()
    {
        // Arrange
        var content = new S3ObjectContentResult
        {
            Bucket = "bucket",
            Key = "file.json",
            Content = "{\"data\":\"test\"}"
        };
        _s3Service.GetObjectContentAsync("bucket", "file.json", true, 262144, Arg.Any<CancellationToken>())
            .Returns(content);

        // Act
        var result = await _sut.GetObjectContent("bucket", "file.json", cancellationToken: CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(content, ok.Value);
    }

    // --- SyncFromBatch ---

    [Fact]
    public async Task SyncFromBatch_MissingFields_ReturnsBadRequest()
    {
        // Arrange
        var request = new SyncS3FromBatchRequest { QueueType = "", BatchId = "" };

        // Act
        var result = await _sut.SyncFromBatch(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SyncFromBatch_BatchNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new SyncS3FromBatchRequest { QueueType = "orders", BatchId = "missing" };
        _storageService.BuildBatchPath("orders", "missing").Returns("/nonexistent/path");

        // Act
        var result = await _sut.SyncFromBatch(request, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SyncFromBatch_ValidBatch_SyncsAndReturnsCount()
    {
        // Arrange
        var batchPath = Path.Combine(Path.GetTempPath(), "test-s3sync-" + Guid.NewGuid());
        Directory.CreateDirectory(batchPath);
        try
        {
            var request = new SyncS3FromBatchRequest
            {
                QueueType = "orders",
                BatchId = "batch1",
                UseAwsFallback = true
            };
            var messages = new List<SavedMessage>
            {
                new() { MessageId = "m1", Body = "body" }
            };
            _storageService.BuildBatchPath("orders", "batch1").Returns(batchPath);
            _storageService.LoadBatchAsync(batchPath).Returns(messages);
            _s3Service.SyncS3ObjectsForMessagesAsync(messages, true, Arg.Any<CancellationToken>())
                .Returns(1);

            // Act
            var result = await _sut.SyncFromBatch(request, CancellationToken.None);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }
        finally
        {
            Directory.Delete(batchPath, true);
        }
    }

    // ── UploadObject Tests ────────────────────────────────────────

    [Fact]
    public async Task UploadObject_ValidRequest_ReturnsOkWithETag()
    {
        // Arrange
        var request = new UploadS3ObjectRequest
        {
            Key = "orders/order-123.json",
            Content = "{\"id\":\"123\"}",
            ContentType = "application/json"
        };
        _s3Service.UploadObjectToLocalStackAsync(
                "my-bucket", "orders/order-123.json", "{\"id\":\"123\"}", "application/json", Arg.Any<CancellationToken>())
            .Returns("\"etag-abc\"");

        // Act
        var result = await _sut.UploadObject("my-bucket", request, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UploadS3ObjectResponse>(ok.Value);
        Assert.Equal("my-bucket", response.BucketName);
        Assert.Equal("orders/order-123.json", response.Key);
        Assert.Equal("\"etag-abc\"", response.ETag);
    }

    [Fact]
    public async Task UploadObject_EmptyBucketName_ReturnsBadRequest()
    {
        // Arrange
        var request = new UploadS3ObjectRequest { Key = "file.json", Content = "data" };

        // Act
        var result = await _sut.UploadObject("  ", request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadObject_EmptyKey_ReturnsBadRequest()
    {
        // Arrange
        var request = new UploadS3ObjectRequest { Key = "", Content = "data" };

        // Act
        var result = await _sut.UploadObject("my-bucket", request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadObject_EmptyContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new UploadS3ObjectRequest { Key = "file.json", Content = "" };

        // Act
        var result = await _sut.UploadObject("my-bucket", request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
