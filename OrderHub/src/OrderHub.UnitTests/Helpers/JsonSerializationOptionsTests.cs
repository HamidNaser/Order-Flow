using System.Text.Json;
using OrderHub.Common.Helpers;
using Xunit;

namespace OrderHub.UnitTests.Helpers;

public class JsonSerializationOptionsTests
{
    private enum SampleState
    {
        FirstValue,
        RequiresReview
    }

    private class SamplePayload
    {
        public SampleState CurrentState { get; set; }
    }

    [Fact]
    public void GetJsonSerializerOptions_UsesCamelCaseAndSnakeUpperEnums()
    {
        var options = JsonSerializationOptions.GetJsonSerializerOptions();
        var payload = new SamplePayload { CurrentState = SampleState.RequiresReview };

        var json = JsonSerializer.Serialize(payload, options);

        Assert.Contains("\"currentState\"", json);
        Assert.Contains("\"REQUIRES_REVIEW\"", json);
    }
}
