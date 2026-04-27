using System.Text;
using System.Text.Json;
using Amazon.SQS.Model;
using Order.MessagePump.Messages;
using OrderGateway.Common.Configuration;
using OrderGateway.Common.Configuration.Handlers;
using OrderGateway.Common.Handlers;
using OrderGateway.Common.Managers;
using OrderGateway.Common.Models;
using OrderGateway.Common.Models.Events;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace OrderGateway.UnitTests.Handlers;

public class OrderEventHandlerTests
{
    private readonly IOrderEventManager manager = Substitute.For<IOrderEventManager>();
    private readonly OrderEventHandler handler;

    public OrderEventHandlerTests()
    {
        var opts = Options.Create(new MessageHandlerOptions { MaxMessageRetries = 3 });
        handler = new OrderEventHandler(manager, opts);
    }

    [Fact]
    public void ParseOrderEvent_ShouldReturnExpectedEvent()
    {
        var payload = new OrderEvent
        {
            Type = "order-outbound",
            SubType = "general",
            CreatedOn = DateTime.UtcNow.ToString("O"),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "UserId", "456" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "OrderFlowType", "outbound" },
                { "OrderReferenceId", "e-1" }
            }
        };

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, SerializationConfig.DefaultSettings)));

        var result = handler.ParseEvent(new Message { Body = encoded });

        Assert.Equal("order-outbound", result.Type);
        Assert.Equal(6082, result.StoreId);
        Assert.Equal(123, result.CustomerId);
        Assert.Equal(456, result.UserId);
    }

    [Fact]
    public async Task HandleMessageAsync_ValidEvent_ReturnsComplete()
    {
        var payload = new OrderEvent
        {
            Type = "order-outbound",
            CreatedOn = DateTime.UtcNow.ToString("O"),
            Metadata = new Dictionary<string, string>
            {
                { "StoreId", "6082" },
                { "CustomerId", "123" },
                { "UserId", "456" },
                { "SenderAddress", "STORE-ORD-10001" },
                { "RecipientAddress", "CUST-ORD-78901" },
                { "OrderFlowType", "outbound" },
                { "OrderReferenceId", "e-1" }
            }
        };

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, SerializationConfig.DefaultSettings)));
        var message = new Message { Body = encoded };

        manager.ProcessEvent(Arg.Any<OrderEvent>()).Returns(ProcessingResult.Complete());

        var result = await handler.HandleMessageAsync(message);

        Assert.Equal(MessageResultAction.Complete, result.Action);
        await manager.Received(1).ProcessEvent(Arg.Any<OrderEvent>());
    }

    [Fact]
    public async Task HandleMessageAsync_InvalidBody_ReturnsPoison()
    {
        var result = await handler.HandleMessageAsync(new Message { Body = "not-base64" });

        Assert.Equal(MessageResultAction.Poison, result.Action);
    }
}
