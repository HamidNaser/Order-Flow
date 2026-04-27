using OrderHub.Contracts;
using Xunit;

namespace OrderHub.UnitTests.OrderContracts;

public class PhoneNumberValidationAttributeTests
{
    private readonly PhoneNumberValidationAttribute _validator = new();

    [Theory]
    [InlineData("2025551234")]
    [InlineData("3105551234")]
    [InlineData("4155551234")]
    [InlineData("2125551234")]
    public void IsValid_ValidUS10DigitNumber_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Expected '{phoneNumber}' to be valid");
    }

    [Theory]
    [InlineData("202-555-1234")]
    [InlineData("(310) 555-1234")]
    [InlineData("+1 202 555 1234")]
    [InlineData("+1-310-555-1234")]
    [InlineData("202.555.1234")]
    [InlineData("310 555 1234")]
    public void IsValid_ValidFormattedUSNumber_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Expected '{phoneNumber}' to be valid");
    }

    [Theory]
    [InlineData("+12025551234")]
    [InlineData("+13105551234")]
    [InlineData("+14155551234")]
    public void IsValid_ValidE164Format_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Expected '{phoneNumber}' to be valid");
    }

    [Theory]
    [InlineData("+441234567890")]
    [InlineData("+33123456789")]
    [InlineData("+8108012345678")]
    [InlineData("+44 20 1234 56")]
    public void IsValid_ValidInternationalNumber_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Expected '{phoneNumber}' to be valid");
    }

    [Theory]
    [InlineData("abcdefghij")]
    [InlineData("abc-def-ghij")]
    [InlineData("555-abc-1234")]
    [InlineData("202-CALL-NOW")]
    [InlineData("1-800-FLOWERS")]
    public void IsValid_PhoneNumberWithLetters_ReturnsFalse(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.False(result, $"Expected '{phoneNumber}' to be invalid (contains letters)");
    }

    [Theory]
    [InlineData("555-123")]
    [InlineData("12345")]
    [InlineData("123")]
    public void IsValid_PartialPhoneNumber_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Expected '{phoneNumber}' to be valid (partial numbers accepted)");
    }

    [Theory]
    [InlineData("5551234567")]
    [InlineData("555-123-4567")]
    [InlineData("+15551234567")]
    [InlineData("(555) 123-4567")]
    public void IsValid_555ExchangeNumber_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Expected '{phoneNumber}' to be valid (555 exchange accepted)");
    }

    [Theory]
    [InlineData("1234567890123456")]
    [InlineData("123456789012345678")]
    [InlineData("+1234567890123456")]
    [InlineData("+12345678901234567")]
    [InlineData("+123456789012345678")]
    public void IsValid_PhoneNumberTooLong_ReturnsFalse(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.False(result, $"Expected '{phoneNumber}' to be invalid (exceeds length limit)");
    }

    [Theory]
    [InlineData("123456789012345")]
    [InlineData("(202) 555-1234")]
    [InlineData("+123456789012345")]
    public void IsValid_LengthBoundaryValid_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Expected '{phoneNumber}' to be valid (at or under length limit)");
    }

    [Fact]
    public void IsValid_Null_ReturnsTrue()
    {
        // Act
        var result = _validator.IsValid(null);

        // Assert
        Assert.True(result, "Null should be considered valid (let [Required] handle it separately)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsValid_EmptyOrWhitespace_ReturnsTrue(string phoneNumber)
    {
        // Act
        var result = _validator.IsValid(phoneNumber);

        // Assert
        Assert.True(result, $"Empty/whitespace should be considered valid (let [Required] handle it separately)");
    }
}

