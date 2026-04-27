using OrderHub.Contracts;
using Xunit;

namespace OrderHub.UnitTests.OrderContracts;

public class AddressValidationAttributeTests
{
    private readonly AddressValidationAttribute _validator = new();

    [Theory]
    [InlineData("ORD-12345")]
    [InlineData("customer-78901")]
    [InlineData("STORE_001_ACCT")]
    [InlineData("CUST-ORD-99887")]
    [InlineData("acct:98765")]
    [InlineData("dock-bay-7A")]
    public void IsValid_ValidAddressFormat_ReturnsTrue(string address)
    {
        // Act
        var result = _validator.IsValid(address);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("\"Doug Maxwell\" <STORE-AGT-001>")]
    [InlineData("\"FirstName, LastName (OrgName - Location)\" <CUST-ORD-002>")]
    [InlineData("\"John Doe\" <ORD-RECV-5501>")]
    [InlineData("Display Name <STORE-ORD-10001>")]
    [InlineData("\"Test User\" <AGT-TST-003>")]
    [InlineData("\"Name, With Comma\" <DOCK-7A-RCV>")]
    [InlineData("CUST-ORD-002<CUST-ORD-002>")]
    [InlineData("\"Unclosed Quote <STORE-AGT-001>")]
    [InlineData("No Brackets STORE-ORD-10001")]
    [InlineData("\"CUST-ORD-002\"<CUST-ORD-002>")]
    public void IsValid_CompositeFormatAddress_ReturnsFalse(string compositeAddress)
    {
        // Act
        var result = _validator.IsValid(compositeAddress);

        // Assert
        Assert.False(result, $"Expected '{compositeAddress}' to be invalid");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("x")]
    public void IsValid_TooShortIdentifier_ReturnsFalse(string shortAddress)
    {
        // Act
        var result = _validator.IsValid(shortAddress);

        // Assert
        Assert.False(result, $"Expected '{shortAddress}' to be invalid - too short");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValid_EmptyNullOrWhitespaceValue_ReturnsTrue(string? value)
    {
        // Arrange - empty/whitespace values should pass (use [Required] separately)

        // Act
        var result = _validator.IsValid(value);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("ORD-12345-ABCDE")]
    [InlineData("STORE-AGT-001-X")]
    [InlineData("C.O.D")]
    [InlineData("DOCK-7A-RECV.01")]
    public void IsValid_LenientFormatsWithSpecialCharacters_ReturnsTrue(string address)
    {
        // Act
        var result = _validator.IsValid(address);

        // Assert
        Assert.True(result, $"Expected '{address}' to be valid");
    }
}
