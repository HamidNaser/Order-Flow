using OrderHub.Common.Helpers;
using Xunit;

namespace OrderHub.UnitTests.Helpers;

public class PhoneNumberHelperTests
{
    [Theory]
    [InlineData("+19135551212", "+19135551212")]
    [InlineData("9135551212", "+19135551212")]
    public void Normalize_ValidNumber_ReturnsE164(string input, string expected)
    {
        var result = PhoneNumberHelper.Normalize(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("not-a-phone")]
    public void Normalize_InvalidOrShort_ReturnsOriginal(string input)
    {
        var result = PhoneNumberHelper.Normalize(input);

        Assert.Equal(input, result);
    }
}
