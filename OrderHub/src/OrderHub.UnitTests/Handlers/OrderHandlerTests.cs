using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Order.MessagePump.Messages;
using OrderHub.Common.Configuration.Queues;
using OrderHub.Common.Handlers;
using OrderHub.Common.Models;
using OrderHub.Common.Models.OrderMappers;
using OrderHub.Common.Repositories;
using OrderHub.Common.Services;
using OrderHub.Common.Telemetry;
using OrderHub.Contracts.Ingest;
using OrderHub.Contracts.Utility;
using Xunit;
using Priority = OrderHub.Common.Models.Components.Priority;
using OrderFlowType = OrderHub.Contracts.Common.Enums.OrderFlowType;
using MerchantName = OrderHub.Contracts.Common.Enums.MerchantName;
using ChannelType = OrderHub.Contracts.Common.Enums.ChannelType;
using FulfillmentStatus = OrderHub.Contracts.Common.Enums.FulfillmentStatus;

namespace OrderHub.UnitTests.Handlers;

public class OrderHandlerTests
{
    private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
    private readonly IContentProcessingService _contentProcessingService = Substitute.For<IContentProcessingService>();
    private readonly IOrderMapper _orderMapper = Substitute.For<IOrderMapper>();
    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();
    private readonly ICustomerLockService _customerLockService = Substitute.For<ICustomerLockService>();
    private readonly IOrderMetrics _metrics = Substitute.For<IOrderMetrics>();
    private readonly OrderHandler _sut;

    private const string ValidBucket = "test-bucket";
    private const string ValidOrderId = "64f1234567890abcdef12345";
    private const string ValidKey = $"STANDARD/PRIME/STANDARD/source123/{ValidOrderId}";

    public OrderHandlerTests()
    {
        var options = Options.Create(new MessageHandlerOptions { MaxMessageRetries = 3 });
        _sut = new OrderHandler(
            _s3Service,
            _contentProcessingService,
            _orderMapper,
            _repository,
            _customerLockService,
            _metrics,
            options
        );
    }

    // ──────────────────────────────────────────────
    // Happy path — full order processing
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ValidOrder_CompletesSuccessfully()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);
        var orderRequest = CreateOrderRequest();
        var channelOrder = CreateShipmentOrder();
        var lease = CreateAcquiredLease();

        _s3Service.GetObjectAsync<OrderRequest>(ValidBucket, ValidKey)
            .Returns(new S3GetObjectResponse<OrderRequest> { Content = orderRequest, ErrorType = S3ErrorType.NONE });

        _contentProcessingService.ProcessContent(Arg.Any<ChannelType>(), Arg.Any<string>())
            .Returns(new ContentProcessingResult("preview", 10, 10, 10));

        _orderMapper.ToInternalModel(
                Arg.Any<OrderRequest>(), Arg.Any<string>(),
                Arg.Any<ContentProcessingResult>(), Arg.Any<Priority>())
            .Returns(channelOrder);

        _customerLockService.AcquireLocksAsync(Arg.Any<IEnumerable<string>>())
            .Returns(lease);

        _repository.InsertAsync(Arg.Any<ChannelOrder>()).Returns(channelOrder);

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        await _repository.Received(1).InsertAsync(channelOrder);
        await _customerLockService.Received(1).ReleaseLocksAsync(lease);
    }

    // ──────────────────────────────────────────────
    // S3 error classification (the transient retry fix)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_S3NotFound_PoisonsMessage()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);

        _s3Service.GetObjectAsync<OrderRequest>(ValidBucket, ValidKey)
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                ErrorType = S3ErrorType.NOT_FOUND,
                ErrorMessage = "Key not found"
            });

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    [Fact]
    public async Task HandleMessageAsync_S3ParsingError_PoisonsMessage()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);

        _s3Service.GetObjectAsync<OrderRequest>(ValidBucket, ValidKey)
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                ErrorType = S3ErrorType.PARSING_ERROR,
                ErrorMessage = "Invalid JSON"
            });

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    [Fact]
    public async Task HandleMessageAsync_S3UnexpectedError_RetriesMessage()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);

        _s3Service.GetObjectAsync<OrderRequest>(ValidBucket, ValidKey)
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                ErrorType = S3ErrorType.UNEXPECTED,
                ErrorMessage = "Service Unavailable"
            });

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
    }

    [Fact]
    public async Task HandleMessageAsync_S3NullContent_PoisonsMessage()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);

        _s3Service.GetObjectAsync<OrderRequest>(ValidBucket, ValidKey)
            .Returns(new S3GetObjectResponse<OrderRequest>
            {
                Content = null,
                ErrorType = S3ErrorType.NONE
            });

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    // ──────────────────────────────────────────────
    // Lock acquisition failure — retry
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_LockNotAcquired_RetriesMessage()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);
        SetupSuccessfulS3Response();

        var notAcquiredLease = Substitute.For<ICustomerLockLease>();
        notAcquiredLease.IsAcquired.Returns(false);
        _customerLockService.AcquireLocksAsync(Arg.Any<IEnumerable<string>>())
            .Returns(notAcquiredLease);

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Retry, result.Action);
        await _repository.DidNotReceiveWithAnyArgs().InsertAsync(default!);
    }

    // ──────────────────────────────────────────────
    // Lock release — always called even on exception
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_RepositoryThrows_StillReleasesLock()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);
        SetupSuccessfulS3Response();

        var lease = CreateAcquiredLease();
        _customerLockService.AcquireLocksAsync(Arg.Any<IEnumerable<string>>())
            .Returns(lease);

        _repository.InsertAsync(Arg.Any<ChannelOrder>())
            .ThrowsAsync(new InvalidOperationException("db error"));

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert — lock released even though repo threw
        await _customerLockService.Received(1).ReleaseLocksAsync(lease);
        Assert.Equal(MessageResultAction.Retry, result.Action);
    }

    // ──────────────────────────────────────────────
    // Parsing failures
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_InvalidS3EventJson_PoisonsMessage()
    {
        // Arrange
        var message = new Message { Body = "not-valid-json {{{" };

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    [Fact]
    public async Task HandleMessageAsync_InvalidKeyFormat_PoisonsMessage()
    {
        // Arrange — valid S3 event but key has wrong segment count
        var message = CreateS3EventMessage(ValidBucket, "invalid/key");

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    [Fact]
    public async Task HandleMessageAsync_EmptyBucketName_PoisonsMessage()
    {
        // Arrange
        var message = CreateS3EventMessage("", ValidKey);

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    // ──────────────────────────────────────────────
    // S3 test event — complete immediately
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_S3TestEvent_CompletesWithoutProcessing()
    {
        // Arrange
        var message = new Message
        {
            Body = """{"Event":"s3:TestEvent","Bucket":{"Name":"test-bucket"}}"""
        };

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        await _s3Service.DidNotReceiveWithAnyArgs().GetObjectAsync<OrderRequest>(default!, default!);
    }

    // ──────────────────────────────────────────────
    // Retry + max retries — poison after threshold
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ExceedsMaxRetries_PoisonsMessage()
    {
        // Arrange
        var message = CreateS3EventMessage(ValidBucket, ValidKey);
        message.Attributes = new Dictionary<string, string>
        {
            ["ApproximateReceiveCount"] = "10" // exceeds max of 3
        };
        SetupSuccessfulS3Response();

        var notAcquiredLease = Substitute.For<ICustomerLockLease>();
        notAcquiredLease.IsAcquired.Returns(false);
        _customerLockService.AcquireLocksAsync(Arg.Any<IEnumerable<string>>())
            .Returns(notAcquiredLease);

        // Act
        var result = await _sut.HandleMessageAsync(message);

        // Assert — retry result gets escalated to poison because of max retries
        Assert.Equal(MessageResultAction.Poison, result.Action);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static Message CreateS3EventMessage(string bucket, string key)
    {
        var body = $$"""
        {
            "Records": [
                {
                    "s3": {
                        "bucket": { "name": "{{bucket}}" },
                        "object": { "key": "{{key}}" }
                    }
                }
            ]
        }
        """;

        return new Message { Body = body };
    }

    private void SetupSuccessfulS3Response()
    {
        var orderRequest = CreateOrderRequest();
        var channelOrder = CreateShipmentOrder();

        _s3Service.GetObjectAsync<OrderRequest>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new S3GetObjectResponse<OrderRequest> { Content = orderRequest, ErrorType = S3ErrorType.NONE });

        _contentProcessingService.ProcessContent(Arg.Any<ChannelType>(), Arg.Any<string>())
            .Returns(new ContentProcessingResult("preview", 10, 10, 10));

        _orderMapper.ToInternalModel(
                Arg.Any<OrderRequest>(), Arg.Any<string>(),
                Arg.Any<ContentProcessingResult>(), Arg.Any<Priority>())
            .Returns(channelOrder);
    }

    private static OrderRequest CreateOrderRequest()
    {
        return new AddShipmentOrderRequest
        {
            CustomerId = "customer-123",
            CustomerName = "Test Customer",
            StoreId = "store-1",
            OrderFlow = OrderFlowType.INCOMING,
            Content = "Test content",
            OrderPlacedDate = DateTimeOffset.UtcNow,
            Merchant = new OrderHub.Contracts.Common.Merchant
            {
                Name = MerchantName.PRIME,
                OrderId = "source123"
            },
            FulfillmentStatus = FulfillmentStatus.SUCCESS,
            OrderFulfilledDate = DateTimeOffset.UtcNow,
            To = [new OrderHub.Contracts.Common.AddressInfo { Address = "addr1" }],
            From = new OrderHub.Contracts.Common.AddressInfo { Address = "addr2" }
        };
    }

    private static ShipmentOrder CreateShipmentOrder()
    {
        return new ShipmentOrder
        {
            OrderId = ValidOrderId,
            CustomerId = "customer-123",
            StoreId = "store-1",
            OrderPlacedDate = DateTimeOffset.UtcNow,
            OrderFlow = (OrderHub.Common.Models.Components.OrderFlowType)OrderFlowType.INCOMING,
            Merchant = new OrderHub.Common.Models.Components.Merchant
            {
                Name = OrderHub.Common.Models.Components.MerchantName.PRIME,
                OrderId = "source123"
            },
            FulfillmentStatus = OrderHub.Common.Models.Components.FulfillmentStatus.SUCCESS,
            Priority = Priority.STANDARD,
            CreatedDate = DateTimeOffset.UtcNow,
            UpdatedDate = DateTimeOffset.UtcNow,
            To = [new OrderHub.Common.Models.Components.AddressInfo { Address = "addr1" }],
            From = new OrderHub.Common.Models.Components.AddressInfo { Address = "addr2" }
        };
    }

    private static ICustomerLockLease CreateAcquiredLease()
    {
        var lease = Substitute.For<ICustomerLockLease>();
        lease.IsAcquired.Returns(true);
        return lease;
    }
}
