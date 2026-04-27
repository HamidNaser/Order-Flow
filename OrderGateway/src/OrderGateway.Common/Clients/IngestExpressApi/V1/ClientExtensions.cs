namespace OrderGateway.Common.Clients.IngestExpressApi.V1;

public static class IngestExpressClientExtensions
{
    public static IIngestExpressClient WithCorrelationId(this IIngestExpressClient client, string? correlationId)
    {
        if (client is IngestExpressClient concreteClient && !string.IsNullOrWhiteSpace(correlationId))
        {
            concreteClient.CustomCorrelationId = correlationId;                        
        }
        return client;
    }
}

public partial class IngestExpressClient
{
    internal string? CustomCorrelationId { get; set; }

    partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url)
    {
        if (!string.IsNullOrWhiteSpace(CustomCorrelationId))
        {
            request.Headers.TryAddWithoutValidation("X-Order-Correlation-Id", CustomCorrelationId);
        }
    }
}
