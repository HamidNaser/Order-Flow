using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Order.MessagePump.Publishers
{
    public interface IPublisherClient
    {
        Task<string> PublishMessageAsync(string body, Dictionary<string, string>? attributes = null, CancellationToken cancellationToken = default);
    }
}
