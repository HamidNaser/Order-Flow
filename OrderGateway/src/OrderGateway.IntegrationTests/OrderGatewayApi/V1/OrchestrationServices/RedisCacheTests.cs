using OrderGateway.IntegrationTests.Clients.OrderGatewayApi.V1.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace OrderGateway.IntegrationTests.OrderGatewayApi.V1.OrchestrationServices;

[Collection("ApiTests")]
public class RedisCacheTests(ApiTestsFixture fixture)
{
    [Fact]
    public async Task TestRedisCrud_WithHappyPath_ReturnHappyPath()
    {
        var key = Guid.NewGuid().ToString();
        var value = Guid.NewGuid().ToString();

        var request = new RedisCacheSetRequest { Key = key, Value = value };
        var setResponse = await fixture.OrderGatewayApiV1Client.SetRedisCacheAsync(request);
        Assert.Equal(StatusCodes.Status204NoContent, setResponse.StatusCode);

        var getResponse = await fixture.OrderGatewayApiV1Client.GetRedisCacheAsync(key);
        Assert.Equal(StatusCodes.Status200OK, getResponse.StatusCode);
        Assert.Equal(value, getResponse.Result.Value);

        var deleteResponse = await fixture.OrderGatewayApiV1Client.DeleteRedisCacheAsync(key);
        Assert.Equal(StatusCodes.Status204NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Get_NonExistentKey_ReturnsNullValue()
    {
        var key = Guid.NewGuid().ToString();
        var getResponse = await fixture.OrderGatewayApiV1Client.GetRedisCacheAsync(key);
        Assert.Equal(StatusCodes.Status200OK, getResponse.StatusCode);
        Assert.Null(getResponse.Result.Value);
    }

    [Fact]
    public async Task Delete_NonExistentKey_ReturnsNoContent()
    {
        var key = Guid.NewGuid().ToString();
        var deleteResponse = await fixture.OrderGatewayApiV1Client.DeleteRedisCacheAsync(key);
        Assert.Equal(StatusCodes.Status204NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Set_OverwriteExistingKey_UpdatesValue()
    {
        var key = Guid.NewGuid().ToString();
        var value1 = Guid.NewGuid().ToString();
        var value2 = Guid.NewGuid().ToString();

        var request1 = new RedisCacheSetRequest { Key = key, Value = value1 };
        var setResponse1 = await fixture.OrderGatewayApiV1Client.SetRedisCacheAsync(request1);
        Assert.Equal(StatusCodes.Status204NoContent, setResponse1.StatusCode);

        var request2 = new RedisCacheSetRequest { Key = key, Value = value2 };
        var setResponse2 = await fixture.OrderGatewayApiV1Client.SetRedisCacheAsync(request2);
        Assert.Equal(StatusCodes.Status204NoContent, setResponse2.StatusCode);

        var getResponse = await fixture.OrderGatewayApiV1Client.GetRedisCacheAsync(key);
        Assert.Equal(StatusCodes.Status200OK, getResponse.StatusCode);
        Assert.Equal(value2, getResponse.Result.Value);

        var deleteResponse = await fixture.OrderGatewayApiV1Client.DeleteRedisCacheAsync(key);
        Assert.Equal(StatusCodes.Status204NoContent, deleteResponse.StatusCode);
    }
}
