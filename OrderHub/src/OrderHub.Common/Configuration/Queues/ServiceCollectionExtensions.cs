using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Configuration.Queues;
using OrderHub.Common.Handlers;
using OrderHub.Common.Services;
using Order.MessagePump.Aws.Queues;
using Order.MessagePump.Handlers;
using Order.MessagePump.Pipelines;
using Order.MessagePump.Publishers;
using Order.MessagePump.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureQueues(
        this IServiceCollection services,
        IConfiguration configuration,
        List<Queues> queues
    )
    {
        var awsConnectionOptions = configuration
            .GetSection("Aws:Connection")
            .Get<AwsConnectionOptions>();

        var clientOptions = Enum
            .GetValues<Queues>()
            .ToDictionary(
                sq => sq,
                sq => configuration
                    .GetSection($"QueueClientOptions:{sq}")
                    .Get<SqsQueueClientOptions>()
            );

        services.AddSingleton(clientOptions);

        var workerOptions = Enum
            .GetValues<Queues>()
            .ToDictionary(
                sq => sq,
                sq => configuration
                    .GetSection($"QueueMessageWorkerOptions:{sq}")
                    .Get<QueueMessageWorkerOptions>()
            );

        services.AddSingleton(workerOptions);

        var clients = GetClients(queues, clientOptions, awsConnectionOptions);
        services.AddSingleton(clients);

        var workerFactories = new Dictionary<Queues, Func<IServiceProvider, IHostedService>>();

        foreach (var queue in queues)
        {

            workerFactories[queue] = sp => new QueueMessageWorker<Message>(
                sp.GetQueueMessageWorkerOptions(queue),
                sp.GetQueueClient(queue),
                GetHandlerForQueue(sp, queue)
            );
        }

        services.AddSingleton(workerFactories);

        return services;
    }

    private static IServiceCollection ConfigureHandlers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MessageHandlerOptions>()
            .Bind(configuration.GetSection("MessageHandlerOptions"))
            .ValidateOnStart();

        services.AddSingleton<OrderHandler>();

        return services;
    }

    private static IMessageHandler<Message> GetHandlerForQueue(IServiceProvider serviceProvider, Queues queue)
    {
        return queue switch
        {
            Queues.EXPRESS => serviceProvider.GetRequiredService<OrderHandler>(),
            Queues.STANDARD => serviceProvider.GetRequiredService<OrderHandler>(),
            _ => throw new ArgumentOutOfRangeException(nameof(queue), queue, "Unsupported queue type")
        };
    }

    private static Dictionary<Queues, IQueueClient<Message>> GetClients(
        List<Queues> queues,
        Dictionary<Queues, SqsQueueClientOptions?> clientOptions,
        AwsConnectionOptions? awsConnectionOptions
    )
    {
        var sqsClient = CreateSqsClient(awsConnectionOptions);

        var clients = clientOptions
            .Where(x => queues.Contains(x.Key))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Region == "local"
                    ? new LocalQueueClient()
                    : new SqsQueueClient(new SqsQueueClientOptions
                    {
                        QueueName = kvp.Value.QueueName,
                        PoisonQueueName = $"{kvp.Value.QueueName}-deadletter",
                        WaitTimeSeconds = kvp.Value.WaitTimeSeconds
                    }, sqsClient) as IQueueClient<Message>
            );

        return clients;
    }

    private static AmazonSQSClient CreateSqsClient(AwsConnectionOptions? options = null)
    {
        if (options != null)
        {
            var config = new AmazonSQSConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region
            };

            // Use anonymous credentials for LocalStack to avoid reading .aws\credentials
            var credentials = !string.IsNullOrWhiteSpace(options.ServiceUrl)
                ? new AnonymousAWSCredentials()  // LocalStack - don't read .aws\credentials
                : null;  // Production - use default credential chain

            return credentials != null
                ? new AmazonSQSClient(credentials, config)
                : new AmazonSQSClient(config);
        }

        return new AmazonSQSClient();  // Default AWS behavior
    }

    private static Dictionary<Queues, IPublisherClient> GetPublishers(
        Dictionary<Queues, SqsQueueClientOptions?> clientOptions,
        AwsConnectionOptions? awsConnectionOptions
    )
    {
        var sqsClient = CreateSqsClient(awsConnectionOptions);

        var publishers = clientOptions
            .ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var opts = kvp.Value ?? new SqsQueueClientOptions { Region = "local" };
                    var region = string.IsNullOrWhiteSpace(opts.Region) ? "local" : opts.Region;

                    if (string.Equals(region, "local", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return (IPublisherClient)new LocalQueueClient();
                    }

                    return (IPublisherClient)new SqsQueueClient(new SqsQueueClientOptions
                    {
                        QueueName = opts.QueueName,
                        PoisonQueueName = $"{opts.QueueName}-deadletter",
                        WaitTimeSeconds = opts.WaitTimeSeconds
                    }, sqsClient);
                }
            );

        return publishers;
    }

    private static IServiceCollection ConfigureQueuePublishers(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Get AwsConnectionOptions once from configuration
        var awsConnectionOptions = configuration
            .GetSection("Aws:Connection")
            .Get<AwsConnectionOptions>();

        var clientOptions = Enum
            .GetValues<Queues>()
            .ToDictionary(
                sq => sq,
                sq => configuration
                    .GetSection($"QueueClientOptions:{sq}")
                    .Get<SqsQueueClientOptions>()
            );

        var publishers = GetPublishers(clientOptions, awsConnectionOptions);
        services.AddSingleton(publishers);

        return services;
    }

    private static QueueMessageWorkerOptions GetQueueMessageWorkerOptions(
        this IServiceProvider serviceProvider,
        Queues queue
    ) =>
        serviceProvider.GetRequiredService<Dictionary<Queues, QueueMessageWorkerOptions>>()[queue];

    private static IQueueClient<Message> GetQueueClient(
        this IServiceProvider serviceProvider,
        Queues queue
    ) =>
        serviceProvider.GetRequiredService<Dictionary<Queues, IQueueClient<Message>>>()[queue];

    public static IPublisherClient GetQueuePublisherClient(
        this IServiceProvider serviceProvider,
        Queues queue
    ) =>
        serviceProvider.GetRequiredService<Dictionary<Queues, IQueueClient<Message>>>()[queue] as IPublisherClient
            ?? throw new InvalidOperationException($"Queue {queue} does not have a publisher client.");

    public static IServiceCollection StartQueueMessageWorker(
        this IServiceCollection services,
        Queues queue
    ) =>
        services.AddSingleton<IHostedService>(serviceProvider =>
            serviceProvider
                .GetRequiredService<Dictionary<Queues, Func<IServiceProvider, IHostedService>>>()[queue]
                .Invoke(serviceProvider)
        );
}
