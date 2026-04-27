namespace OrderGateway.Common.Configuration
{
    public class ResourceServerApiKeyOptions
    {
        public string HeaderName { get; init; } = "x-api-key";
        public required string ApiKey { get; init; }
        public required string ResourceBaseAddress { get; init; }
        public int TimeoutSeconds { get; init; } = 10;
    }
}
