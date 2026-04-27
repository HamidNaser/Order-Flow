using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderGateway.Common.Configuration
{
    public static class SerializationConfig
    {
        public static readonly JsonSerializerOptions? DefaultSettings = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        static SerializationConfig()
        {
            DefaultSettings.Converters.Add(new JsonStringEnumConverter());
            DefaultSettings.Converters.Add(new JsonUtcDateTimeConverter());
        }
    }
}
