using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Controllers.V1;
using Order.MessageOperations.Api.Models;
using Order.MessageOperations.Api.Models.Responses;
using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace Order.MessageOperations.Api.Tests.Controllers.V1;

public class HealthControllerTests
{
    private readonly IQueueReplayService _queueReplayService;
    private readonly IS3OperationsService _s3Service;
    private readonly HealthController _sut;

    public HealthControllerTests()
    {
        var options = Options.Create(new MessageOperationsOptions
        {
            LocalStackEndpoint = "http://localhost:4566"
        });

        _queueReplayService = Substitute.For<IQueueReplayService>();
        _s3Service = Substitute.For<IS3OperationsService>();
        _sut = new HealthController(_queueReplayService, _s3Service, options);
    }

    [Fact]
    public async Task CheckLocalStackHealth_AllHealthy_ReturnsOk()
    {
        // Arrange
        _queueReplayService.ListLocalStackQueuesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "queue-1", "queue-2" });
        _s3Service.ListBucketsAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<S3BucketInfo>
            {
                new() { Name = "bucket-1", CreationDate = DateTime.UtcNow }
            });

        // Act
        var result = await _sut.CheckLocalStackHealth(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LocalStackHealthResponse>(ok.Value);
        Assert.True(response.Healthy);
        Assert.True(response.Sqs.Healthy);
        Assert.True(response.S3.Healthy);
        Assert.Equal("http://localhost:4566", response.LocalStackEndpoint);
    }

    [Fact]
    public async Task CheckLocalStackHealth_SqsUnhealthy_Returns503()
    {
        // Arrange
        _queueReplayService.ListLocalStackQueuesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection refused"));
        _s3Service.ListBucketsAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<S3BucketInfo>());

        // Act
        var result = await _sut.CheckLocalStackHealth(CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
        var response = Assert.IsType<LocalStackHealthResponse>(statusResult.Value);
        Assert.False(response.Healthy);
        Assert.False(response.Sqs.Healthy);
        Assert.Contains("Connection refused", response.Sqs.Detail);
        Assert.True(response.S3.Healthy);
    }

    [Fact]
    public async Task CheckLocalStackHealth_S3Unhealthy_Returns503()
    {
        // Arrange
        _queueReplayService.ListLocalStackQueuesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        _s3Service.ListBucketsAsync(true, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("S3 unavailable"));

        // Act
        var result = await _sut.CheckLocalStackHealth(CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
        var response = Assert.IsType<LocalStackHealthResponse>(statusResult.Value);
        Assert.False(response.Healthy);
        Assert.True(response.Sqs.Healthy);
        Assert.False(response.S3.Healthy);
        Assert.Contains("S3 unavailable", response.S3.Detail);
    }

    [Fact]
    public async Task CheckLocalStackHealth_BothUnhealthy_Returns503()
    {
        // Arrange
        _queueReplayService.ListLocalStackQueuesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SQS error"));
        _s3Service.ListBucketsAsync(true, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("S3 error"));

        // Act
        var result = await _sut.CheckLocalStackHealth(CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
        var response = Assert.IsType<LocalStackHealthResponse>(statusResult.Value);
        Assert.False(response.Healthy);
        Assert.False(response.Sqs.Healthy);
        Assert.False(response.S3.Healthy);
    }
}
