using OrderHub.Common.Utilities;
using Xunit;

namespace OrderHub.UnitTests.Helpers;

public class Base64UrlTextEncoderHelperTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_ReturnsOriginal()
    {
        const string input = "hello+world/with=symbols";

        var encoded = Base64UrlTextEncoderHelper.Encode(input);
        var decoded = Base64UrlTextEncoderHelper.Decode(encoded);

        Assert.NotEqual(input, encoded);
        Assert.Equal(input, decoded);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EncodeDecode_EmptyOrNull_ReturnsInput(string? input)
    {
        Assert.Equal(input, Base64UrlTextEncoderHelper.Encode(input!));
        Assert.Equal(input, Base64UrlTextEncoderHelper.Decode(input!));
    }

    [Fact]
    public void Decode_InvalidInput_ReturnsEmptyString()
    {
        var result = Base64UrlTextEncoderHelper.Decode("%%%not-base64url%%%");

        Assert.Equal(string.Empty, result);
    }
}
