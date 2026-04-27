using OrderGateway.Common.Models.Events;
using Xunit;

namespace OrderGateway.UnitTests.Models
{
    public class OrderEventValidationTests
    {
        [Theory]
        [InlineData("\"Doug Maxwell\" <STORE-AGT-001>", true)]
        [InlineData("\"FirstName, LastName (OrgName - Location)\" <CUST-ORD-002>", true)]
        [InlineData("STORE-ORD-10001", true)]
        [InlineData("order-tracking-ref", true)]
        [InlineData("CUST.ORD.001+tag", true)]
        [InlineData("\"Store Agent\" <AGT-001>", true)]
        [InlineData("\"AGT-001\" <AGT-001>", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("\"Agent Name\" <>", false)]
        [InlineData("<>", false)]
        [InlineData("< >", false)]
        public void IsValid_WithVariousAddressFormats_ValidatesCorrectly(string address, bool expectedValid)
        {
            // Arrange
            var orderEvent = CreateValidOrderEvent();
            orderEvent.Metadata!["SenderAddress"] = address;
            orderEvent.Metadata["RecipientAddress"] = "CUST-ORD-78901";

            // Act
            var isValid = orderEvent.IsValid();

            // Assert
            Assert.Equal(expectedValid, isValid);
        }

        [Theory]
        [InlineData("\"Doug Maxwell\" <STORE-AGT-001>", true)]
        [InlineData("\"FirstName, LastName (OrgName - Location)\" <CUST-ORD-002>", true)]
        [InlineData("STORE-ORD-10001", true)]
        [InlineData("order-tracking-ref", true)]
        [InlineData("CUST.ORD.001+tag", true)]
        [InlineData("\"Store Agent\" <AGT-001>", true)]
        [InlineData("\"AGT-001\" <AGT-001>", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("\"Agent Name\" <>", false)]
        [InlineData("<>", false)]
        [InlineData("< >", false)]
        public void IsValid_WithVariousToAddressFormats_ValidatesCorrectly(string toAddress, bool expectedValid)
        {
            // Arrange
            var orderEvent = CreateValidOrderEvent();
            orderEvent.Metadata!["SenderAddress"] = "STORE-ORD-10001";
            orderEvent.Metadata["RecipientAddress"] = toAddress;

            // Act
            var isValid = orderEvent.IsValid();

            // Assert
            Assert.Equal(expectedValid, isValid);
        }

        

        [Fact]
        public void IsValid_WithMissingSenderAddress_ReturnsFalse()
        {
            // Arrange
            var orderEvent = CreateValidOrderEvent();
            orderEvent.Metadata!.Remove("SenderAddress");
            orderEvent.Metadata["RecipientAddress"] = "CUST-ORD-78901";

            // Act
            var isValid = orderEvent.IsValid();

            // Assert
            Assert.False(isValid, "Should reject when SenderAddress is missing");
        }

        [Fact]
        public void IsValid_WithMissingRecipientAddress_ReturnsFalse()
        {
            // Arrange
            var orderEvent = CreateValidOrderEvent();
            orderEvent.Metadata!["SenderAddress"] = "STORE-ORD-10001";
            orderEvent.Metadata.Remove("RecipientAddress");

            // Act
            var isValid = orderEvent.IsValid();

            // Assert
            Assert.False(isValid, "Should reject when RecipientAddress is missing");
        }

        private static OrderEvent CreateValidOrderEvent()
        {
            return new OrderEvent
            {
                Type = "order-outbound",
                SubType = "general",
                Description = "Test order",
                CreatedOn = DateTime.UtcNow.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    { "StoreId", "6082" },
                    { "CustomerId", "12345" },
                    { "OrderReferenceId", "test-order-ref-id" },
                    { "SenderAddress", "STORE-ORD-10001" },
                    { "RecipientAddress", "CUST-ORD-78901" },
                    { "OrderFlowType", "outbound" },
                    { "OrderTitle", "Test Order Title" }
                }
            };
        }
    }
}