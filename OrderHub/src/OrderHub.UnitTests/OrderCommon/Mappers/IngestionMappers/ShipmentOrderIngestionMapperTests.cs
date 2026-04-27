using OrderHub.Common.Models;
using OrderHub.Common.Models.OrderMappers.IngestionMappers;
using OrderHub.Common.Models.Components;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;
using OrderHub.UnitTests.Helpers.TestDataBuilders;
using Xunit;

namespace OrderHub.UnitTests.OrderCommon.Mappers.IngestionMappers;

public class ShipmentOrderIngestionMapperTests
{
    private readonly ShipmentOrderIngestionMapper _mapper = new();

    [Fact]
    public void ToInternalModel_ValidOrderRequest_ReturnsCorrectShipmentOrder()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Test content preview";
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentPreview.Length, contentPreview.Length, contentPreview.Length);
        const Priority priority = Priority.EXPRESS;

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            priority
        );

        // Assert
        Assert.IsType<ShipmentOrder>(result);
        var shipmentResult = (ShipmentOrder)result;

        // Verify basic properties
        Assert.Equal(orderId, shipmentResult.OrderId);
        Assert.Equal(contentProcessingResult.OrderSummary, shipmentResult.OrderSummary);
        Assert.Equal(request.TenantId, shipmentResult.TenantId);
        Assert.Equal(request.StoreId, shipmentResult.StoreId);
        Assert.Equal(request.OrderPlacedDate, shipmentResult.OrderPlacedDate);
        Assert.Equal(request.OrderFulfilledDate, shipmentResult.OrderFulfilledDate);
        Assert.Equal((OrderFlowType)request.OrderFlow, shipmentResult.OrderFlow);
        Assert.Equal((FulfillmentStatus)request.FulfillmentStatus, shipmentResult.FulfillmentStatus);
        Assert.Equal(priority, shipmentResult.Priority);

        // Verify mapped objects
        Assert.Equal(request.CustomerId, shipmentResult.CustomerId);
        Assert.Equal(request.CustomerName, shipmentResult.CustomerName);
        Assert.Equal(request.AgentId, shipmentResult.AgentId);
        Assert.Equal(request.AgentName, shipmentResult.AgentName);
        Assert.Equal((MerchantName)request.Merchant.Name, shipmentResult.Merchant.Name);
        Assert.Equal(request.Merchant.OrderId, shipmentResult.Merchant.OrderId);
        Assert.Equal(request.Merchant.SourceApplication, shipmentResult.Merchant.SourceApplication);
        Assert.Equal((PlatformId?)request.Platform?.Id, shipmentResult.Platform?.Id);
        Assert.Equal(request.Platform?.OperationId, shipmentResult.Platform?.OperationId);

        // Verify shipment-specific properties
        Assert.Single(shipmentResult.To);
        Assert.Equal(request.To.Single().Address, shipmentResult.To.Single().Address);
        Assert.Equal(request.To.Single().Name, shipmentResult.To.Single().Name);
        Assert.Equal(request.From.Address, shipmentResult.From.Address);
        Assert.Equal(request.From.Name, shipmentResult.From.Name);

        Assert.Equal(request.OrderTitle, shipmentResult.OrderTitle);

        // Verify timestamps are recent
        Assert.True(shipmentResult.CreatedDate >= DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.True(shipmentResult.UpdatedDate >= DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(shipmentResult.CreatedDate, shipmentResult.UpdatedDate);

        // Verify OrderMetadata
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.Empty(shipmentResult.OrderMetadata.MediaIds);
        Assert.Equal(contentPreview.Length, shipmentResult.OrderMetadata.ContentLength);
        Assert.Equal(contentPreview.Length, shipmentResult.OrderMetadata.VisibleContentLength);
        Assert.Equal(contentPreview.Length, shipmentResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_OrderRequestWithNullOrderTitle_HandlesNullCorrectly()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        request.OrderTitle = null!;
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Test content preview";
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentPreview.Length, contentPreview.Length, contentPreview.Length);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.STANDARD
        );

        // Assert
        var shipmentResult = (ShipmentOrder)result;
        Assert.Null(shipmentResult.OrderTitle);

        // Verify OrderMetadata
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.Empty(shipmentResult.OrderMetadata.MediaIds);
        Assert.Equal(contentPreview.Length, shipmentResult.OrderMetadata.ContentLength);
        Assert.Equal(contentPreview.Length, shipmentResult.OrderMetadata.VisibleContentLength);
        Assert.Equal(contentPreview.Length, shipmentResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_TextRequest_ThrowsArgumentException()
    {
        // Arrange
        var wrongRequest = ContractTestDataBuilder.CreateDefaultAddDigitalOrderRequest();
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Test content preview";
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentPreview.Length, contentPreview.Length, contentPreview.Length);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _mapper.ToInternalModel(wrongRequest, orderId, contentProcessingResult, Priority.EXPRESS));

        Assert.Contains(nameof(AddShipmentOrderRequest), exception.Message);
        Assert.Contains(nameof(AddDigitalOrderRequest), exception.Message);
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void ToInternalModel_WithMediaIds_MapsMediaIdsCorrectly()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        request.MediaIds = ["media123", "media456", "media789"];
        request.Content = "<p>Hello World</p>";
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Hello World";
        var contentLength = request.Content.Length;
        var visibleLength = contentPreview.Length;
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentLength, visibleLength, visibleLength);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var shipmentResult = (ShipmentOrder)result;
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.NotNull(shipmentResult.OrderMetadata.MediaIds);
        Assert.Equal(3, shipmentResult.OrderMetadata.MediaIds.Count);
        Assert.Contains("media123", shipmentResult.OrderMetadata.MediaIds);
        Assert.Contains("media456", shipmentResult.OrderMetadata.MediaIds);
        Assert.Contains("media789", shipmentResult.OrderMetadata.MediaIds);
        Assert.Equal(contentLength, shipmentResult.OrderMetadata.ContentLength);
        Assert.Equal(visibleLength, shipmentResult.OrderMetadata.VisibleContentLength);
        Assert.Equal(visibleLength, shipmentResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_WithNullMediaIds_SetsEmptyList()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        request.MediaIds = null;
        request.Content = "Test content";
        var orderId = "64f1234567890abcdef12345";
        var content = request.Content;
        var contentProcessingResult = new ContentProcessingResult(content, content.Length, content.Length, content.Length);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var shipmentResult = (ShipmentOrder)result;
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.NotNull(shipmentResult.OrderMetadata.MediaIds);
        Assert.Empty(shipmentResult.OrderMetadata.MediaIds);
        Assert.Equal(content.Length, shipmentResult.OrderMetadata.ContentLength);
        Assert.Equal(content.Length, shipmentResult.OrderMetadata.VisibleContentLength);
        Assert.Equal(content.Length, shipmentResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_WithContent_CalculatesContentLengthCorrectly()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        request.Content = "<p>Hello <b>World</b>!</p>";
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Hello World!";
        var contentLength = request.Content.Length;
        var visibleLength = contentPreview.Length;
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentLength, visibleLength, visibleLength);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var shipmentResult = (ShipmentOrder)result;
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.Equal(26, shipmentResult.OrderMetadata.ContentLength); // Full HTML length
        Assert.Equal(12, shipmentResult.OrderMetadata.VisibleContentLength); // Text without HTML
        Assert.Equal(12, shipmentResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_WithNullContent_SetsContentLengthToZero()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        request.Content = null;
        var orderId = "64f1234567890abcdef12345";
        var contentProcessingResult = new ContentProcessingResult(string.Empty, 0, 0, 0);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var shipmentResult = (ShipmentOrder)result;
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.Equal(0, shipmentResult.OrderMetadata.ContentLength);
        Assert.Equal(0, shipmentResult.OrderMetadata.VisibleContentLength);
        Assert.Equal(0, shipmentResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_WithMarkupContent_CalculatesPlainTextContentLengthCorrectly()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        request.Content = "<html><body><p>Hello <b>World</b>!</p></body></html>";
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Hello World!";
        var contentLength = request.Content.Length;
        var visibleLength = contentPreview.Length;
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentLength, visibleLength, visibleLength);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var shipmentResult = (ShipmentOrder)result;
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.Equal(52, shipmentResult.OrderMetadata.ContentLength); // Full HTML length
        Assert.NotNull(shipmentResult.OrderMetadata.PlainTextContentLength);
        Assert.Equal(12, shipmentResult.OrderMetadata.PlainTextContentLength.Value); // markup-stripped length
        Assert.Equal(12, shipmentResult.OrderMetadata.VisibleContentLength);
    }

    [Fact]
    public void ToInternalModel_WithNullContent_SetsPlainTextContentLengthToZero()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        request.Content = null;
        var orderId = "64f1234567890abcdef12345";
        var contentProcessingResult = new ContentProcessingResult(string.Empty, 0, 0, 0);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var shipmentResult = (ShipmentOrder)result;
        Assert.NotNull(shipmentResult.OrderMetadata);
        Assert.NotNull(shipmentResult.OrderMetadata.PlainTextContentLength);
        Assert.Equal(0, shipmentResult.OrderMetadata.PlainTextContentLength.Value);
        Assert.Equal(0, shipmentResult.OrderMetadata.ContentLength);
        Assert.Equal(0, shipmentResult.OrderMetadata.VisibleContentLength);
    }
}

