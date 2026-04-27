namespace OrderGateway.Common.Clients.IngestStandardApi.V1;

public static class IngestStandardClientExtensions
{
    public static IIngestStandardClient WithCorrelationId(this IIngestStandardClient client, string? correlationId)
    {
        if (client is IngestStandardClient concreteClient && !string.IsNullOrWhiteSpace(correlationId))
        {
            concreteClient.CustomCorrelationId = correlationId;
        }
        return client;
    }
}

public partial class IngestStandardClient
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
