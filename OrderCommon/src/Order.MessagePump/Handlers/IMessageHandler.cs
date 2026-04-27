using Order.MessagePump.Messages;
using System.Threading.Tasks;

namespace Order.MessagePump.Handlers
{
    public interface IMessageHandler<TMessage>
    {
        Task<MessageResult> HandleMessageAsync(TMessage message);
    }
}
