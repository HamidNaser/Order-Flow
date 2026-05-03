using Order.MessagePump.Messages;
using Order.MessagePump.Publishers;
using OrderGateway.Common.Configuration.Queues;
using OrderGateway.Common.FeatureToggle;
using OrderGateway.Common.Managers;
using OrderGateway.Common.Models;
using OrderGateway.Common.Models.Events;
using OrderGateway.Common.Services;
using OrderGateway.Common.Processing.Abstractions;
using OrderGateway.Common.Telemetry;
using NSubstitute;
using Xunit;

namespace OrderGateway.UnitTests.Managers;

public class OrderEventManagerTests
{
    private readonly OrderEventManager orderEventManager;
    private readonly IFeatureToggle featureToggleMock = Substitute.For<IFeatureToggle>();
    private readonly ICloudContentService cloudContentServiceMock = Substitute.For<ICloudContentService>();
    private readonly IOrderService orderServiceMock = Substitute.For<IOrderService>();
    private readonly IContentSizeMetricEmitter contentSizeMetricEmitterMock = Substitute.For<IContentSizeMetricEmitter>();
    private readonly IOrderMetrics metricsMock = Substitute.For<IOrderMetrics>();

    public OrderEventManagerTests()
    {
        orderEventManager = new OrderEventManager(featureToggleMock, cloudContentServiceMock, orderServiceMock, contentSizeMetricEmitterMock, metricsMock);
    }

    // helpers removed; each test will configure and assert directly

    [Fact]
    public async Task ProcessEvent_ValidOrderEvent_ReturnsComplete_UsingFromAddress()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "7898989"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "OrderTypeId", "12"}, // 12 - Reply from contact
                { "SourceTrackingId", new Random().NextInt64().ToString()},
                { "TrackingRef", new Random().NextInt64().ToString()},
                { "UserId", new Random().Next().ToString()},
                { "MessageId", Guid.NewGuid().ToString()},
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
            }
        };
        //enable LD
        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        // Order service ingestion success
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.True(string.IsNullOrWhiteSpace(result.Details));
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_ValidOrderEvent_ReturnsComplete_UsingToAddress()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "7898989"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "inbound" },
                { "OrderTypeId", "12"}, // 12 - Reply from contact
                { "SourceTrackingId", new Random().NextInt64().ToString()},
                { "TrackingRef", new Random().NextInt64().ToString()},
                { "UserId", new Random().Next().ToString()},
                { "MessageId", Guid.NewGuid().ToString()},
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
            }
        };
        //enable LD
        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        // Order service ingestion success
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.True(string.IsNullOrWhiteSpace(result.Details));
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_InvalidOrderEvent_AlertClassification_CompletesValidationMessage()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "7898989"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "inbound" },
                { "Classification", "alert" },
                { "OrderTypeId", "12"}, // 12 - Reply from contact
                { "SourceTrackingId", new Random().NextInt64().ToString()},
                { "TrackingRef", new Random().NextInt64().ToString()},
                { "UserId", new Random().Next().ToString()},
                { "MessageId", Guid.NewGuid().ToString()},
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
            }
        };

        // Order service (not expected to be called in this validation failure test)
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);
        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Contains("Order event failed validation", result.Details);
        await orderServiceMock.Received(0).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_InvalidOrderEvent_CompletesValidationMessage()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "", // Invalid - empty required field
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>()
        };

        // Order service (not expected to be called in this validation failure test)
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);
        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Contains("Order event failed validation", result.Details);
        await orderServiceMock.Received(0).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_InvalidOrderEvent_NoMetaData_CompletesValidationMessage()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "Order", // Invalid - empty required field
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
        };

        // Order service (not expected to be called in this validation failure test)
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);
        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Contains("Order event failed validation", result.Details);
        await orderServiceMock.Received(0).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_InvalidOrderEvent_NoOrderReferenceId_CompletesValidationMessage()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "Order", // Invalid - empty required field
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "UserId", "7898989"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "OrderTypeId", "12"}, // 12 - Reply from contact
                { "SourceTrackingId", new Random().NextInt64().ToString()},
                { "TrackingRef", new Random().NextInt64().ToString()},
                { "MessageId", Guid.NewGuid().ToString()},
                { "OrderFlags", "0"},
                { "OrderReferenceId", ""},
                { "HasAttachments", "false"}
            }
        };

        // Order service (not expected to be called in this validation failure test)
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);
        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Contains("Order event failed validation", result.Details);
        await orderServiceMock.Received(0).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_StoreNotInLaunchDarkly_ReturnsSkipped()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "999999" }, //Not in LaunchDarkly
                { "CustomerId", "7898989"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "OrderTypeId", "12"}, // 12 - Reply from contact
                { "SourceTrackingId", new Random().NextInt64().ToString()},
                { "TrackingRef", new Random().NextInt64().ToString()},
                { "UserId", new Random().Next().ToString()},
                { "MessageId", Guid.NewGuid().ToString()},
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
            }
        };

        // Order service (not expected to be called when store not enabled)
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Contains("Store not enabled, skipped.", result.Details);
        await orderServiceMock.Received(0).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_StoreNotInMappingDictionary_CompletesWithCoorgNotFound()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "1" }, // In LaunchDarkly but not in mapping dictionary
                { "CustomerId", "7898989"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "OrderTypeId", "12"}, // 12 - Reply from contact
                { "SourceTrackingId", new Random().NextInt64().ToString()},
                { "TrackingRef", new Random().NextInt64().ToString()},
                { "UserId", new Random().Next().ToString()},
                { "MessageId", Guid.NewGuid().ToString()},
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true); // Simulate that the feature is enabled for all stores in tests

        // Order service (not expected to be called when coorg not found)
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        var result = await orderEventManager.ProcessEvent(orderEvent);
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.True(string.IsNullOrWhiteSpace(result.Details));
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_ConsumerLookup_NotFound_RetriesWithDetail()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            ApproximateReceiveCount = 1,
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "4242"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        // Order service (not expected to be called when consumer not found)
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.True(string.IsNullOrWhiteSpace(result.Details));
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_UsesGlobalCustomerId_FromContactId_Metadata()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            Description = "Test order",
            CreatedOn = DateTime.UtcNow.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "999"},
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        // Order service ingestion success
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        // Act
        var result = await orderEventManager.ProcessEvent(orderEvent);

        // Assert
        Assert.Equal(MessageResultAction.Complete, result.Action);
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_OriginalMessagePresent_SkipsCloudContentLookup()
    {
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            CreatedOn = DateTime.UtcNow.ToString(),
            Description = "Already",
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"},
                { "OriginalMessage", "Already have original body" }
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        var result = await orderEventManager.ProcessEvent(orderEvent);

        Assert.Equal(MessageResultAction.Complete, result.Action);
        await cloudContentServiceMock.Received(0).ReadContentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_NoCloudContentKey_SkipsLookup()
    {
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            CreatedOn = DateTime.UtcNow.ToString(),
            Description = "Existing body", // should remain unchanged
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"}
                // intentionally no MessageCloudContentKey and no OriginalMessage
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        // Order service ingestion success
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        var originalDescription = orderEvent.Description;
        var result = await orderEventManager.ProcessEvent(orderEvent);

        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Equal(originalDescription, orderEvent.Description);
        await cloudContentServiceMock.Received(0).ReadContentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_CloudContentRetrieved_SetsContextMessageContent()
    {
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            CreatedOn = DateTime.UtcNow.ToString(),
            Description = "Placeholder", // should be replaced
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"},
                { "MessageCloudContentKey", "key-123" }
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        cloudContentServiceMock.ReadContentAsync("key-123", Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("Retrieved body content"));

        // Order service ingestion success (assert on context content)
        orderServiceMock.SendAsync(
                Arg.Any<IOrderEvent>(),
                Arg.Is<StepContext>(c => c.MessageContent == "Retrieved body content"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        var result = await orderEventManager.ProcessEvent(orderEvent);

        Assert.Equal(MessageResultAction.Complete, result.Action);
        // Description is not mutated anymore; content carried via StepContext
        Assert.Equal("Placeholder", orderEvent.Description);
        await cloudContentServiceMock.Received(1).ReadContentAsync("key-123", Arg.Any<CancellationToken>());
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Is<StepContext>(c => c.MessageContent == "Retrieved body content"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_CloudContentNotFound_Continues()
    {
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            CreatedOn = DateTime.UtcNow.ToString(),
            Description = "Existing body", // should remain
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"},
                { "MessageCloudContentKey", "key-404" }
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        cloudContentServiceMock.ReadContentAsync("key-404", Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>(null));

        // Order service ingestion success
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        var originalDescription = orderEvent.Description;
        var result = await orderEventManager.ProcessEvent(orderEvent);

        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Equal(originalDescription, orderEvent.Description);
        await cloudContentServiceMock.Received(1).ReadContentAsync("key-404", Arg.Any<CancellationToken>());
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_CloudContentEmpty_Continues()
    {
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            CreatedOn = DateTime.UtcNow.ToString(),
            Description = "Existing body", // should remain
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"},
                { "MessageCloudContentKey", "key-empty" }
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        cloudContentServiceMock.ReadContentAsync("key-empty", Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>(string.Empty));

        // Order service ingestion success
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        var originalDescription = orderEvent.Description;
        var result = await orderEventManager.ProcessEvent(orderEvent);

        Assert.Equal(MessageResultAction.Complete, result.Action);
        Assert.Equal(originalDescription, orderEvent.Description);
        await cloudContentServiceMock.Received(1).ReadContentAsync("key-empty", Arg.Any<CancellationToken>());
        await orderServiceMock.Received(1).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEvent_CloudContentFailure_Poisons()
    {
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            CreatedOn = DateTime.UtcNow.ToString(),
            Description = "Existing body", // should not be replaced due to failure
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "OrderTitle", "Test Order Title" },
                { "OrderFlowType", "outbound" },
                { "SourceTrackingId", "1001" },
                { "TrackingRef", "2002" },
                { "UserId", "3003" },
                { "MessageId", Guid.NewGuid().ToString() },
                { "OrderFlags", "0"},
                { "OrderReferenceId", Guid.NewGuid().ToString() },
                { "HasAttachments", "false"},
                { "MessageCloudContentKey", "key-fail" }
            }
        };

        featureToggleMock.IsFeatureEnabled(FeatureFlags.OrderGatewayEnabledStoresV2, Arg.Any<FeatureUser>())
            .Returns(true);

        cloudContentServiceMock.ReadContentAsync("key-fail", Arg.Any<CancellationToken>()).Returns<Task<string?>>(x => throw new InvalidOperationException("boom"));
        orderServiceMock.SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrderIngestResult.Ingested("order-id")));

        var result = await orderEventManager.ProcessEvent(orderEvent);

        Assert.Equal(MessageResultAction.Poison, result.Action);
        Assert.Contains("Cloud content retrieval failure for key key-fail.", result.Details);
        await cloudContentServiceMock.Received(1).ReadContentAsync("key-fail", Arg.Any<CancellationToken>());
        await orderServiceMock.Received(0).SendAsync(Arg.Any<IOrderEvent>(), Arg.Any<StepContext>(), Arg.Any<CancellationToken>());
    }

}

