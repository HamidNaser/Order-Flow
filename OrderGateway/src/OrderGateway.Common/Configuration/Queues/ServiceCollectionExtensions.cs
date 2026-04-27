using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Order.MessagePump.Aws.Queues;
using Order.MessagePump.Pipelines;
using Order.MessagePump.Publishers;
using Order.MessagePump.Queues;
using OrderGateway.Common.Configuration.AppSettings;
using OrderGateway.Common.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SupportedQueues = OrderGateway.Common.Configuration.Queues.SupportedQueues;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureQueues(this IServiceCollection services, IConfiguration configuration)
    {
        // AWS Configuration - read ServiceUrl for LocalStack support
        var awsOptions = configuration.GetSection("Aws").Get<AwsOptions>() ?? new AwsOptions();
        services.Configure<AwsOptions>(configuration.GetSection("Aws"));

        // Options
        var clientOptions = Enum.GetValues<SupportedQueues>()
            .ToDictionary(q => q, q => configuration.GetSection($"QueueClientOptions:{q}").Get<SqsQueueClientOptions>());

        services.AddSingleton(clientOptions);

        //Refer QueueMessageWorkerOptions in MessagePump for more settings that we can use in case of troubleshooting.
        var workerOptions = Enum.GetValues<SupportedQueues>()
            .ToDictionary(q => q, q => configuration.GetSection($"QueueMessageWorkerOptions:{q}").Get<QueueMessageWorkerOptions>());

        services.AddSingleton(workerOptions);

        // Clients - configure SQS client with LocalStack support
        var sqsConfig = new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(awsOptions.Connection.Region)
        };

        // Override endpoint for LocalStack
        if (!string.IsNullOrWhiteSpace(awsOptions.Connection.ServiceUrl))
        {
            sqsConfig.ServiceURL = awsOptions.Connection.ServiceUrl;
        }

        // Use anonymous credentials for LocalStack to avoid reading .aws\credentials
        var credentials = !string.IsNullOrWhiteSpace(awsOptions.Connection.ServiceUrl)
            ? new AnonymousAWSCredentials()  // LocalStack - don't read .aws\credentials
            : null;  // Production - use default credential chain

        var sqsClient = credentials != null
            ? new AmazonSQSClient(credentials, sqsConfig)
            : new AmazonSQSClient(sqsConfig);
        var clients = clientOptions.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var opts = kvp.Value ?? new SqsQueueClientOptions { Region = "local" };
                var region = string.IsNullOrWhiteSpace(opts.Region) ? "local" : opts.Region;

                if (string.Equals(region, "local", StringComparison.InvariantCultureIgnoreCase))
                {
                    return (IQueueClient<Message>)new LocalQueueClient();
                }

                return (IQueueClient<Message>)new SqsQueueClient(new SqsQueueClientOptions
                {
                    QueueName = opts.QueueName,
                    PoisonQueueName = $"{opts.QueueName}-deadletter",
                    WaitTimeSeconds = opts.WaitTimeSeconds
                }, sqsClient);
            });

        services.AddSingleton(clients);


        // Publishers
        var publishers = clients.ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value as IPublisherClient);

        services.AddSingleton(publishers);

        // Workers
        var workers = new Dictionary<SupportedQueues, Func<IServiceProvider, IHostedService>>()
        {
            {
                SupportedQueues.IncomingOrders,  (sp) => new QueueMessageWorker<Message>(
                sp.GetQueueMessageWorkerOptions(SupportedQueues.IncomingOrders),
                sp.GetQueueClient(SupportedQueues.IncomingOrders),
                sp.GetRequiredService<OrderEventHandler>() )
            }
        };

        services.AddSingleton(workers);

        return services;
    }

    private static QueueMessageWorkerOptions GetQueueMessageWorkerOptions(this IServiceProvider serviceProvider, SupportedQueues queue) =>
        serviceProvider.GetRequiredService<Dictionary<SupportedQueues, QueueMessageWorkerOptions>>()[queue];

    private static IQueueClient<Message> GetQueueClient(this IServiceProvider serviceProvider, SupportedQueues queue) =>
        serviceProvider.GetRequiredService<Dictionary<SupportedQueues, IQueueClient<Message>>>()[queue];

    public static IServiceCollection StartQueueMessageWorker(this IServiceCollection services, SupportedQueues queue) =>
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<Dictionary<SupportedQueues, Func<IServiceProvider, IHostedService>>>()[queue].Invoke(serviceProvider));
}

