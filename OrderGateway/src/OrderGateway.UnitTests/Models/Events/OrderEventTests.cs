using OrderGateway.Common.Models.Events;
using Xunit;

namespace OrderGateway.UnitTests.Models.Events;

public class OrderEventTests
{
    [Fact]
    public void GetValidationErrors_WithNullMetadata_ValidatesTypeProperty()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            Metadata = null
        };

        // Act
        var errors = orderEvent.GetValidationErrors();

        // Assert
        Assert.Single(errors);
        Assert.Contains("Metadata is null", errors);
    }

    [Fact]
    public void GetValidationErrors_WithNullMetadataAndMissingType_ReturnsMultipleErrors()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "", // Invalid
            Metadata = null // Invalid
        };

        // Act
        var errors = orderEvent.GetValidationErrors();

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("Type is missing"));
        Assert.Contains(errors, e => e.Contains("Metadata is null"));
    }

    [Fact]
    public void GetValidationErrors_WithMultipleMetadataErrors_ReturnsAllErrors()
    {
        // Arrange
        var orderEvent = new OrderEvent
        {
            Type = "order-outbound",
            Metadata = new Dictionary<string, string>
            {
                // Missing: StoreId, ContactId, OrderReferenceId, RecipientAddress, SenderAddress, OrderFlowType
            }
        };

        // Act
        var errors = orderEvent.GetValidationErrors();

        // Assert
        Assert.True(errors.Count >= 6, $"Expected at least 6 errors, got {errors.Count}");
        Assert.Contains(errors, e => e.Contains("StoreId"));
        Assert.Contains(errors, e => e.Contains("CustomerId"));
        Assert.Contains(errors, e => e.Contains("OrderReferenceId"));
        Assert.Contains(errors, e => e.Contains("RecipientAddress"));
        Assert.Contains(errors, e => e.Contains("SenderAddress"));
        Assert.Contains(errors, e => e.Contains("OrderFlowType"));
    }
}
