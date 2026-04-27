using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.MessagePump.Publishers
{
    public interface IBatchPublisherClient
    {
        Task<List<PublishResult>> PublishBatchMessagesAsync(List<PublishEntry> entries);
    }
}
