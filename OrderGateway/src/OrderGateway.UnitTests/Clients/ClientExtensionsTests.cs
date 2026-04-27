using OrderGateway.Common.Clients.IngestStandardApi.V1;
using OrderGateway.Common.Clients.IngestExpressApi.V1;
using Xunit;

namespace OrderGateway.UnitTests.Clients;

public class ClientExtensionsTests
{
    [Fact]
    public void IngestStandardClient_WithCorrelationId_SetsCustomCorrelationId()
    {
        var client = new IngestStandardClient("https://test.example.com", new HttpClient());
        var correlationId = "test-standard-correlation-123";

        var result = client.WithCorrelationId(correlationId);

        Assert.NotNull(result);
        Assert.Same(client, result);
        Assert.Equal(correlationId, client.CustomCorrelationId);
    }

    [Fact]
    public void IngestExpressClient_WithCorrelationId_SetsCustomCorrelationId()
    {
        var client = new IngestExpressClient("https://test.example.com", new HttpClient());
        var correlationId = "test-express-correlation-456";

        var result = client.WithCorrelationId(correlationId);

        Assert.NotNull(result);
        Assert.Same(client, result);
        Assert.Equal(correlationId, client.CustomCorrelationId);
    }

    [Fact]
    public void IngestStandardClient_WithCorrelationId_ReturnsFluentInterface()
    {
        var client = new IngestStandardClient("https://test.example.com", new HttpClient());

        var result1 = client.WithCorrelationId("correlation-1");
        var result2 = result1.WithCorrelationId("correlation-2");

        Assert.Same(client, result1);
        Assert.Same(client, result2);
        Assert.Equal("correlation-2", client.CustomCorrelationId);
    }

    [Fact]
    public void IngestExpressClient_WithCorrelationId_ReturnsFluentInterface()
    {
        var client = new IngestExpressClient("https://test.example.com", new HttpClient());

        var result1 = client.WithCorrelationId("correlation-1");
        var result2 = result1.WithCorrelationId("correlation-2");

        Assert.Same(client, result1);
        Assert.Same(client, result2);
        Assert.Equal("correlation-2", client.CustomCorrelationId);
    }
}
