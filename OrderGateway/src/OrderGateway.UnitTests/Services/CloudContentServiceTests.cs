using OrderGateway.Common.Clients.CloudContent.V1;
using OrderGateway.Common.Services;
using NSubstitute;
using Serilog;
using System.Net;
using Xunit;

namespace OrderGateway.UnitTests.Services;

public class CloudContentServiceTests
{
    private readonly ICloudContentClient _client = Substitute.For<ICloudContentClient>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly CloudContentService _service;

    public CloudContentServiceTests()
    {
        _service = new CloudContentService(_client, _logger);
    }

    [Fact]
    public async Task ReadContentAsync_KeyEmpty_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ReadContentAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ReadContentAsync("   "));
    }

    [Fact]
    public async Task ReadContentAsync_Success_ReturnsContent()
    {
        _client.TextGETContentAsync("abc", Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("hello"));
        var result = await _service.ReadContentAsync("abc");
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ReadContentAsync_NotFound_ReturnsNull_WhenExtensionReturnsNull()
    {
        _client.TextGETContentAsync("missing", Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>(null));
        var result = await _service.ReadContentAsync("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadContentAsync_404Exception_CaughtAndReturnsNull()
    {
        var ex = new CloudContentApiV1ClientException("not found", 404, null, null, null);
        _client.TextGETContentAsync("k404", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<string?>(ex));
        var result = await _service.ReadContentAsync("k404");
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadContentAsync_Non404Exception_Bubbles()
    {
        var ex = new CloudContentApiV1ClientException("boom", 500, null, null, null);
        _client.TextGETContentAsync("k500", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<string?>(ex));
        var thrown = await Assert.ThrowsAsync<CloudContentApiV1ClientException>(() => _service.ReadContentAsync("k500"));
        Assert.Equal(500, thrown.StatusCode);
    }

    #region CloudContentClient Extension Tests

    [Fact]
    public async Task TextGETContentAsync_WithValidKey_ReturnsContent()
    {
        var expectedContent = "Hello World";
        var key = "bucket/test.txt";

        var handler = new MockCloudContentTextHttpHandler(HttpStatusCode.OK, expectedContent);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://cloudcontent.example.com") };
        var client = new CloudContentClient(httpClient);

        var result = await client.TextGETContentAsync(key);

        Assert.Equal(expectedContent, result);
        Assert.Contains($"Text/{key}", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task TextGETContentAsync_With404_ReturnsNull()
    {
        var key = "bucket/notfound.txt";

        var handler = new MockCloudContentTextHttpHandler(HttpStatusCode.NotFound, null);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://cloudcontent.example.com") };
        var client = new CloudContentClient(httpClient);

        var result = await client.TextGETContentAsync(key);

        Assert.Null(result);
    }

    [Fact]
    public async Task TextGETContentAsync_WithDoubleQuotes_StripsQuotes()
    {
        var expectedContent = "Content";
        var keyWithQuotes = "\"bucket/test.txt\"";
        var expectedKey = "bucket/test.txt";

        var handler = new MockCloudContentTextHttpHandler(HttpStatusCode.OK, expectedContent);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://cloudcontent.example.com") };
        var client = new CloudContentClient(httpClient);

        var result = await client.TextGETContentAsync(keyWithQuotes);

        Assert.Equal(expectedContent, result);
        Assert.Contains($"Text/{expectedKey}", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task TextGETContentAsync_WithNullKey_ThrowsArgumentException()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://cloudcontent.example.com") };
        var client = new CloudContentClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.TextGETContentAsync(null!));
    }

    #endregion

    #region Mock Handlers

    private class MockCloudContentTextHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _content;

        public Uri? LastRequestUri { get; private set; }

        public MockCloudContentTextHttpHandler(HttpStatusCode statusCode, string? content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(_statusCode);
            if (_statusCode == HttpStatusCode.OK && _content != null)
            {
                response.Content = new StringContent(_content);
            }
            else if (_statusCode != HttpStatusCode.NotFound)
            {
                response.Content = new StringContent("Error");
            }

            return Task.FromResult(response);
        }
    }

    #endregion
}
