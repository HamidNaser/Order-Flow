using NSubstitute;
using OrderHub.Common.Configuration.Channels;
using OrderHub.Common.Services;
using OrderHub.Common.Services.Utils;
using OrderHub.Contracts.Common.Enums;
using Xunit;

namespace OrderHub.UnitTests.Services;

public class ContentProcessingServiceTests
{
    private readonly IHtmlTextExtractorService _htmlExtractor = Substitute.For<IHtmlTextExtractorService>();
    private readonly OrderSummaryConfig _config = new() { TruncateLength = 300 };
    private readonly ContentProcessingService _sut;

    public ContentProcessingServiceTests()
    {
        _sut = new ContentProcessingService(_config, _htmlExtractor);
    }

    // ──────────────────────────────────────────────
    // Channel-type dispatch
    // ──────────────────────────────────────────────

    [Fact]
    public void ProcessContent_StandardChannel_CallsHtmlExtractor()
    {
        // Arrange
        const string rawHtml = "<p>Hello</p>";
        const string extracted = "Hello";
        _htmlExtractor.ExtractText(rawHtml).Returns(extracted);

        // Act
        var result = _sut.ProcessContent(ChannelType.STANDARD, rawHtml);

        // Assert
        _htmlExtractor.Received(1).ExtractText(rawHtml);
        Assert.Equal(extracted, result.OrderSummary);
    }

    [Fact]
    public void ProcessContent_DigitalChannel_DoesNotCallHtmlExtractor()
    {
        // Arrange
        const string content = "Plain text content";

        // Act
        var result = _sut.ProcessContent(ChannelType.DIGITAL, content);

        // Assert
        _htmlExtractor.DidNotReceiveWithAnyArgs().ExtractText(default!);
        Assert.Equal(content, result.OrderSummary);
    }

    // ──────────────────────────────────────────────
    // ContentLength — original content length
    // ──────────────────────────────────────────────

    [Fact]
    public void ProcessContent_ReturnsOriginalContentLength()
    {
        // Arrange
        const string content = "Hello World";
        _htmlExtractor.ExtractText(content).Returns(content);

        // Act
        var result = _sut.ProcessContent(ChannelType.STANDARD, content);

        // Assert
        Assert.Equal(content.Length, result.ContentLength);
    }

    [Fact]
    public void ProcessContent_NullOrWhitespace_ReturnsZeroContentLength()
    {
        // Arrange & Act
        var result = _sut.ProcessContent(ChannelType.DIGITAL, "   ");

        // Assert — whitespace-only content has contentLength = 0
        Assert.Equal(0, result.ContentLength);
        Assert.Equal(string.Empty, result.OrderSummary);
        Assert.Equal(0, result.VisibleContentLength);
    }

    // ──────────────────────────────────────────────
    // Truncation
    // ──────────────────────────────────────────────

    [Fact]
    public void ProcessContent_ShortContent_NoTruncation()
    {
        // Arrange
        const string content = "Short text";

        // Act
        var result = _sut.ProcessContent(ChannelType.DIGITAL, content);

        // Assert
        Assert.Equal(content, result.OrderSummary);
        Assert.Equal(content.Length, result.VisibleContentLength);
    }

    [Fact]
    public void ProcessContent_LongContent_TruncatesToConfiguredLength()
    {
        // Arrange
        var config = new OrderSummaryConfig { TruncateLength = 10 };
        var sut = new ContentProcessingService(config, _htmlExtractor);
        const string content = "This is a very long piece of content";

        // Act
        var result = sut.ProcessContent(ChannelType.DIGITAL, content);

        // Assert
        Assert.Equal(10, result.OrderSummary.Length);
        Assert.Equal(content.Length, result.VisibleContentLength);
    }

    // ──────────────────────────────────────────────
    // PlainTextContentLength — only populated for STANDARD
    // ──────────────────────────────────────────────

    [Fact]
    public void ProcessContent_StandardChannel_SetsPlainTextContentLength()
    {
        // Arrange
        const string rawHtml = "<p>Hello World</p>";
        const string extracted = "Hello World";
        _htmlExtractor.ExtractText(rawHtml).Returns(extracted);

        // Act
        var result = _sut.ProcessContent(ChannelType.STANDARD, rawHtml);

        // Assert
        Assert.Equal(extracted.Length, result.PlainTextContentLength);
    }

    [Fact]
    public void ProcessContent_DigitalChannel_PlainTextContentLengthIsZero()
    {
        // Arrange
        const string content = "Digital content";

        // Act
        var result = _sut.ProcessContent(ChannelType.DIGITAL, content);

        // Assert
        Assert.Equal(0, result.PlainTextContentLength);
    }

    // ──────────────────────────────────────────────
    // Empty / whitespace extracted text
    // ──────────────────────────────────────────────

    [Fact]
    public void ProcessContent_ExtractedTextIsWhitespace_ReturnsEmptySummary()
    {
        // Arrange
        _htmlExtractor.ExtractText(Arg.Any<string>()).Returns("   ");

        // Act
        var result = _sut.ProcessContent(ChannelType.STANDARD, "<p>   </p>");

        // Assert
        Assert.Equal(string.Empty, result.OrderSummary);
        Assert.Equal(0, result.VisibleContentLength);
    }

    [Fact]
    public void ProcessContent_ExtractedTextIsNull_ReturnsEmptySummary()
    {
        // Arrange
        _htmlExtractor.ExtractText(Arg.Any<string>()).Returns((string)null!);

        // Act
        var result = _sut.ProcessContent(ChannelType.STANDARD, "<p></p>");

        // Assert
        Assert.Equal(string.Empty, result.OrderSummary);
        Assert.Equal(0, result.VisibleContentLength);
    }
}
