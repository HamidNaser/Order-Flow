using OrderHub.Common.Configuration.Queues;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static partial class Extensions
{
    public static HostApplicationBuilder AddWorkerDefaults(this HostApplicationBuilder builder, List<Queues> queues)
    {
        builder.Services
            .ConfigureQueues(builder.Configuration, queues)
            .StartQueueMessageWorker(queues.Single());

        return builder;
    }
}
