using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderHub.Common.Helpers
{
    public static class JsonSerializationOptions
    {
        public static JsonSerializerOptions GetJsonSerializerOptions()
        {
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            jsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(
                    JsonNamingPolicy.SnakeCaseUpper,
                    allowIntegerValues: false
                )
            );

            return jsonSerializerOptions;
        }
    }
}
