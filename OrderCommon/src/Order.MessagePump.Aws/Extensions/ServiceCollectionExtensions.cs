using Amazon.SQS;
using Amazon.SQS.Model;
using Order.MessagePump.Aws.Queues;
using Order.MessagePump.Handlers;
using Order.MessagePump.Pipelines;
using Order.MessagePump.Publishers;
using Order.MessagePump.Queues;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Order.MessagePump.Aws.Extensions
{
    /// <summary>
    /// DI extension methods for registering SQS-backed message pump services.
    /// Reduces manual wiring and risk of misconfiguration across consumer teams.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers an <see cref="SqsQueueClient"/> as <see cref="IQueueClient{Message}"/> and <see cref="IPublisherClient"/>
        /// with the specified options and an <see cref="IAmazonSQS"/> client.
        /// </summary>
        public static IServiceCollection AddSqsQueueClient(
            this IServiceCollection services,
            Action<SqsQueueClientOptions> configureOptions,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            var options = new SqsQueueClientOptions();
            configureOptions(options);

            services.Add(new ServiceDescriptor(typeof(SqsQueueClientOptions), _ => options, lifetime));

            services.Add(new ServiceDescriptor(typeof(SqsQueueClient), sp =>
            {
                var sqsClient = sp.GetRequiredService<IAmazonSQS>();
                return new SqsQueueClient(options, sqsClient);
            }, lifetime));

            services.Add(new ServiceDescriptor(typeof(IQueueClient<Message>), sp => sp.GetRequiredService<SqsQueueClient>(), lifetime));
            services.Add(new ServiceDescriptor(typeof(IPublisherClient), sp => sp.GetRequiredService<SqsQueueClient>(), lifetime));

            return services;
        }

        /// <summary>
        /// Registers a <see cref="QueueMessageWorker{TMessage}"/> as a hosted background service
        /// using the provided options.
        /// </summary>
        public static IServiceCollection AddSqsQueueMessageWorker(
            this IServiceCollection services,
            Action<QueueMessageWorkerOptions> configureOptions)
        {
            var options = new QueueMessageWorkerOptions();
            configureOptions(options);

            services.AddSingleton(options);
            services.AddHostedService(sp =>
            {
                var queue = sp.GetRequiredService<IQueueClient<Message>>();
                var handler = sp.GetRequiredService<IMessageHandler<Message>>();
                return new QueueMessageWorker<Message>(options, queue, handler);
            });

            return services;
        }
    }
}
