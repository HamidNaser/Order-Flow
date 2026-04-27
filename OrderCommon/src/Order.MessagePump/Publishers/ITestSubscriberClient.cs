using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.MessagePump.Publishers
{
    public interface ITestSubscriberClient
    {
        Task<List<string>> FindMessagesAsync(string messageContains);
    }
}
