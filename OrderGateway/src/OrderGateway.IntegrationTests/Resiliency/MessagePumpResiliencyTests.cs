using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon;
using Order.MessagePump.Aws.Queues;
using Order.MessagePump.Handlers;
using Order.MessagePump.Messages;
using Order.MessagePump.Pipelines;
using Order.MessagePump.Queues;
using System.Net.Sockets;
using System.Diagnostics;
using Xunit;

namespace OrderGateway.IntegrationTests.Resiliency;

[Collection("ApiTests")]
public class MessagePumpResiliencyTests(ApiTestsFixture fixture)
{
    [Fact]
    public async Task RetryMessageAsync_WithBackoff_HidesMessageUntilVisibilityTimeout()
    {
        var serviceUrl = fixture.Configuration["Aws:Connection:ServiceUrl"];
        if (!string.IsNullOrWhiteSpace(serviceUrl) && !await IsServiceAvailableAsync(serviceUrl))
        {
            return;
        }

        try
        {
            var sqsClient = await CreateSqsClientAsync();
            var queueName = $"it-retry-{Guid.NewGuid():N}";
            var queueUrl = string.Empty;

            try
            {
            var createQueueResponse = await sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = queueName });
            queueUrl = createQueueResponse.QueueUrl;

            await sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = "retry-test"
            });

            var initialReceive = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 1,
                MessageAttributeNames = new List<string> { "All" },
                MessageSystemAttributeNames = new List<string> { "All" }
            });

            var message = Assert.Single(initialReceive.Messages);

            var queueClient = new SqsQueueClient(
                new SqsQueueClientOptions
                {
                    QueueUrl = queueUrl,
                    PoisonQueueUrl = queueUrl,
                    WaitTimeSeconds = 1
                },
                sqsClient);

            await queueClient.RetryMessageAsync(message, TimeSpan.FromSeconds(2));

            var immediateReceive = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 1,
                MessageAttributeNames = new List<string> { "All" },
                MessageSystemAttributeNames = new List<string> { "All" }
            });

            Assert.Empty(immediateReceive.Messages ?? new List<Message>());

            await Task.Delay(TimeSpan.FromMilliseconds(2200));

            var delayedReceive = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 1,
                MessageAttributeNames = new List<string> { "All" },
                MessageSystemAttributeNames = new List<string> { "All" }
            });

            Assert.NotEmpty(delayedReceive.Messages);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(queueUrl))
                {
                    await sqsClient.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = queueUrl });
                }

                sqsClient.Dispose();
            }
        }
        catch (AmazonSQSException ex) when (ex.Message.Contains("security token included in the request is expired", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    }

    private async Task<IAmazonSQS> CreateSqsClientAsync()
    {
        var serviceUrl = fixture.Configuration["Aws:Connection:ServiceUrl"];
        var configuredRegion = fixture.Configuration["Aws:Connection:Region"];
        var region = string.IsNullOrWhiteSpace(configuredRegion) ? "us-east-1" : configuredRegion;

        var sqsConfig = new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            Assert.True(await IsServiceAvailableAsync(serviceUrl), $"Configured SQS endpoint is unreachable: {serviceUrl}");

            sqsConfig.ServiceURL = serviceUrl;
            sqsConfig.AuthenticationRegion = region;
            sqsConfig.UseHttp = serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

            return new AmazonSQSClient(new AnonymousAWSCredentials(), sqsConfig);
        }

        return new AmazonSQSClient(sqsConfig);
    }

    private static async Task<bool> IsServiceAvailableAsync(string serviceUrl)
    {
        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var endpointUri))
        {
            return false;
        }

        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await tcpClient.ConnectAsync(endpointUri.Host, endpointUri.Port, timeoutCts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task QueueMessageWorker_OpensCircuit_AppliesBreakDelay_ThenSkipsHandler()
    {
        var options = new QueueMessageWorkerOptions
        {
            ExceptionsAllowedBeforeBreaking = 1,
            DurationOfBreakSeconds = 1,
            AddMessageToLogContext = false
        };

        var queue = new NoOpQueueClient();
        var handler = new AlwaysFailingHandler();
        var worker = new QueueMessageWorker<string>(options, queue, handler);

        await worker.ProcessMessageAsync("message-1");

        var stopwatch = Stopwatch.StartNew();
        await worker.ProcessMessageAsync("message-2");
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900), $"Expected circuit break delay, actual elapsed: {stopwatch.Elapsed}.");
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task QueueMessageWorker_WhenHandlerReturnsRetry_UsesRetryPathWithBackoff()
    {
        var expectedBackoff = TimeSpan.FromSeconds(3);
        var options = new QueueMessageWorkerOptions
        {
            AddMessageToLogContext = false
        };

        var queue = new RecordingQueueClient();
        var handler = new RetryResultHandler(expectedBackoff);
        var worker = new QueueMessageWorker<string>(options, queue, handler);

        await worker.ProcessMessageAsync("message-1");

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(1, queue.RetryCalls);
        Assert.Equal(0, queue.CompleteCalls);
        Assert.Equal(0, queue.PoisonCalls);
        Assert.Equal(expectedBackoff, queue.LastBackoff);
        Assert.Equal("message-1", queue.LastRetriedMessage);
    }

    private sealed class AlwaysFailingHandler : IMessageHandler<string>
    {
        public int CallCount { get; private set; }

        public Task<MessageResult> HandleMessageAsync(string message)
        {
            CallCount++;
            throw new InvalidOperationException("forced failure");
        }
    }

    private sealed class NoOpQueueClient : IQueueClient<string>
    {
        public Task CompleteMessageAsync(string message) => Task.CompletedTask;

        public Task<List<string>> GetMessagesAsync(int maxNumberOfMessages) => Task.FromResult(new List<string>());

        public Task PoisonMessageAsync(string message, Exception? ex = null, string? reason = null) => Task.CompletedTask;

        public Task RetryMessageAsync(string message, TimeSpan? backoff = null) => Task.CompletedTask;
    }

    private sealed class RetryResultHandler(TimeSpan backoff) : IMessageHandler<string>
    {
        public int CallCount { get; private set; }

        public Task<MessageResult> HandleMessageAsync(string message)
        {
            CallCount++;
            return Task.FromResult(MessageResult.Retry(details: "retry requested", backoff: backoff));
        }
    }

    private sealed class RecordingQueueClient : IQueueClient<string>
    {
        public int RetryCalls { get; private set; }
        public int CompleteCalls { get; private set; }
        public int PoisonCalls { get; private set; }
        public TimeSpan? LastBackoff { get; private set; }
        public string? LastRetriedMessage { get; private set; }

        public Task CompleteMessageAsync(string message)
        {
            CompleteCalls++;
            return Task.CompletedTask;
        }

        public Task<List<string>> GetMessagesAsync(int maxNumberOfMessages) => Task.FromResult(new List<string>());

        public Task PoisonMessageAsync(string message, Exception? ex = null, string? reason = null)
        {
            PoisonCalls++;
            return Task.CompletedTask;
        }

        public Task RetryMessageAsync(string message, TimeSpan? backoff = null)
        {
            RetryCalls++;
            LastBackoff = backoff;
            LastRetriedMessage = message;
            return Task.CompletedTask;
        }
    }
}
