using OrderHub.Common.Helpers;
using OrderHub.Common.Models;
using OrderHub.Common.Models.OrderMappers.IngestionMappers;
using OrderHub.Common.Models.Components;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;
using OrderHub.UnitTests.Helpers.TestDataBuilders;
using Xunit;

namespace OrderHub.UnitTests.OrderCommon.Mappers.IngestionMappers;

public class DigitalOrderIngestionMapperTests
{
    private readonly DigitalOrderIngestionMapper _mapper = new();

    [Fact]
    public void ToInternalModel_ValidTextRequest_ReturnsCorrectDigitalOrder()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddDigitalOrderRequest();
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Test content preview";
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentPreview.Length, contentPreview.Length, 0);
        const Priority priority = Priority.EXPRESS;

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            priority
        );

        // Assert
        Assert.IsType<DigitalOrder>(result);
        var textResult = (DigitalOrder)result;

        // Verify basic properties
        Assert.Equal(orderId, textResult.OrderId);
        Assert.Equal(contentProcessingResult.OrderSummary, textResult.OrderSummary);
        Assert.Equal(request.TenantId, textResult.TenantId);
        Assert.Equal(request.StoreId, textResult.StoreId);
        Assert.Equal(request.OrderPlacedDate, textResult.OrderPlacedDate);
        Assert.Equal(request.OrderFulfilledDate, textResult.OrderFulfilledDate);
        Assert.Equal((OrderFlowType)request.OrderFlow, textResult.OrderFlow);
        Assert.Equal((FulfillmentStatus)request.FulfillmentStatus, textResult.FulfillmentStatus);
        Assert.Equal(priority, textResult.Priority);

        // Verify mapped objects
        Assert.Equal(request.CustomerId, textResult.CustomerId);
        Assert.Equal(request.CustomerName, textResult.CustomerName);
        Assert.Equal(request.AgentId, textResult.AgentId);
        Assert.Equal(request.AgentName, textResult.AgentName);
        Assert.Equal((MerchantName)request.Merchant.Name, textResult.Merchant.Name);
        Assert.Equal(request.Merchant.OrderId, textResult.Merchant.OrderId);
        Assert.Equal(request.Merchant.SourceApplication, textResult.Merchant.SourceApplication);
        Assert.Equal((PlatformId?)request.Platform?.Id, textResult.Platform?.Id);
        Assert.Equal(request.Platform?.OperationId, textResult.Platform?.OperationId);

        // Verify text-specific properties
        Assert.Equal(PhoneNumberHelper.Normalize(request.ToPhoneNumber), textResult.Endpoints.To);
        Assert.Equal(PhoneNumberHelper.Normalize(request.FromPhoneNumber), textResult.Endpoints.From);

        // Verify timestamps are recent
        Assert.True(textResult.CreatedDate >= DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.True(textResult.UpdatedDate >= DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(textResult.CreatedDate, textResult.UpdatedDate);

        // Verify OrderMetadata
        Assert.NotNull(textResult.OrderMetadata);
        Assert.Empty(textResult.OrderMetadata.MediaIds);
        Assert.Equal(contentPreview.Length, textResult.OrderMetadata.ContentLength);
        Assert.Equal(contentPreview.Length, textResult.OrderMetadata.VisibleContentLength);
        Assert.Null(textResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_OrderRequest_ThrowsArgumentException()
    {
        // Arrange
        var wrongRequest = ContractTestDataBuilder.CreateDefaultAddShipmentOrderRequest();
        var orderId = "64f1234567890abcdef12345";
        var contentPreview = "Test content preview";
        var contentProcessingResult = new ContentProcessingResult(contentPreview, contentPreview.Length, contentPreview.Length, 0);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _mapper.ToInternalModel(wrongRequest, orderId, contentProcessingResult, Priority.STANDARD));

        Assert.Contains(nameof(AddDigitalOrderRequest), exception.Message);
        Assert.Contains(nameof(AddShipmentOrderRequest), exception.Message);
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void ToInternalModel_WithMediaIds_MapsMediaIdsCorrectly()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddDigitalOrderRequest();
        request.MediaIds = ["media123", "media456"];
        request.Content = "Hello World";
        var orderId = "64f1234567890abcdef12345";
        var content = request.Content;
        var contentProcessingResult = new ContentProcessingResult(content, content.Length, content.Length, 0);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var textResult = (DigitalOrder)result;
        Assert.NotNull(textResult.OrderMetadata);
        Assert.NotNull(textResult.OrderMetadata.MediaIds);
        Assert.Equal(2, textResult.OrderMetadata.MediaIds.Count);
        Assert.Contains("media123", textResult.OrderMetadata.MediaIds);
        Assert.Contains("media456", textResult.OrderMetadata.MediaIds);
        Assert.Equal(content.Length, textResult.OrderMetadata.ContentLength);
        Assert.Equal(content.Length, textResult.OrderMetadata.VisibleContentLength);
        Assert.Null(textResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_WithNullMediaIds_SetsEmptyList()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddDigitalOrderRequest();
        request.MediaIds = null;
        request.Content = "Test content";
        var orderId = "64f1234567890abcdef12345";
        var content = request.Content;
        var contentProcessingResult = new ContentProcessingResult(content, content.Length, content.Length, 0);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var textResult = (DigitalOrder)result;
        Assert.NotNull(textResult.OrderMetadata);
        Assert.NotNull(textResult.OrderMetadata.MediaIds);
        Assert.Empty(textResult.OrderMetadata.MediaIds);
        Assert.Equal(content.Length, textResult.OrderMetadata.ContentLength);
        Assert.Equal(content.Length, textResult.OrderMetadata.VisibleContentLength);
        Assert.Null(textResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_WithContent_CalculatesContentLengthCorrectly()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddDigitalOrderRequest();
        request.Content = "Hello World!";
        var orderId = "64f1234567890abcdef12345";
        var content = request.Content;
        var contentProcessingResult = new ContentProcessingResult(content, content.Length, content.Length, 0);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var textResult = (DigitalOrder)result;
        Assert.NotNull(textResult.OrderMetadata);
        Assert.Equal(12, textResult.OrderMetadata.ContentLength);
        Assert.Equal(12, textResult.OrderMetadata.VisibleContentLength);
        Assert.Null(textResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_WithNullContent_SetsContentLengthToZero()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddDigitalOrderRequest();
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
        var textResult = (DigitalOrder)result;
        Assert.NotNull(textResult.OrderMetadata);
        Assert.Equal(0, textResult.OrderMetadata.ContentLength);
        Assert.Equal(0, textResult.OrderMetadata.VisibleContentLength);
        Assert.Null(textResult.OrderMetadata.PlainTextContentLength);
    }

    [Fact]
    public void ToInternalModel_PlainTextContentLength_IsNull()
    {
        // Arrange
        var request = ContractTestDataBuilder.CreateDefaultAddDigitalOrderRequest();
        request.Content = "Test content";
        var orderId = "64f1234567890abcdef12345";
        var content = request.Content;
        var contentProcessingResult = new ContentProcessingResult(content, content.Length, content.Length, 0);

        // Act
        var result = _mapper.ToInternalModel(
            request,
            orderId,
            contentProcessingResult,
            Priority.EXPRESS
        );

        // Assert
        var textResult = (DigitalOrder)result;
        Assert.NotNull(textResult.OrderMetadata);
        Assert.Equal(content.Length, textResult.OrderMetadata.ContentLength);
        Assert.Equal(content.Length, textResult.OrderMetadata.VisibleContentLength);
        Assert.Null(textResult.OrderMetadata.PlainTextContentLength);
    }
}

