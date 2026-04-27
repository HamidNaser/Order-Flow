using System.Text.Json;
using System.Text.Json.Serialization;
using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts;
using Xunit.Abstractions;

namespace OrderGateway.IntegrationTests.Support;

internal static class TestOutputHelperExtensions
{
    private static readonly JsonSerializerOptions HandlerResultLogOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
        WriteIndented = true
    };

    public static void WriteHandlerResult(this ITestOutputHelper output, HandlerResultDto? result)
    {
        if (result is null)
        {
            output.WriteLine("HandlerResultDto: <null>");
            return;
        }

        var serializedResult = JsonSerializer.Serialize(result, HandlerResultLogOptions);
        output.WriteLine("HandlerResultDto: {0}", serializedResult);
    }
}
