using Order.MessagePump.Handlers;
using Order.MessagePump.Messages;
using Order.MessagePump.Queues;
using NewRelic.Api.Agent;
using Polly;
using Polly.CircuitBreaker;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static NewRelic.Api.Agent.NewRelic;

namespace Order.MessagePump.Pipelines
{
    public class QueueMessageWorker<TMessage> : MessagePipelineWorkerBase<TMessage> where TMessage : class
    {
        private readonly QueueMessageWorkerOptions options;
        private readonly IQueueClient<TMessage> queue;
        private readonly IMessageHandler<TMessage> handler;

        private readonly AsyncCircuitBreakerPolicy policy;

        public QueueMessageWorker(
            QueueMessageWorkerOptions options,
            IQueueClient<TMessage> queue,
            IMessageHandler<TMessage> handler) : base(options)
        {
            this.options = options;
            this.queue = queue;
            this.handler = handler;

            policy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: options.ExceptionsAllowedBeforeBreaking,
                    durationOfBreak: TimeSpan.FromSeconds(options.DurationOfBreakSeconds),
                    onBreak: (ex, TimeSpan) => Log
                        .ForContext<QueueMessageWorker<TMessage>>()
                        .Error(ex, $"{nameof(QueueMessageWorker<TMessage>)} circuit broken"),
                    onReset: () => Log
                        .ForContext<QueueMessageWorker<TMessage>>()
                        .Information($"{nameof(QueueMessageWorker<TMessage>)} circuit reset"));
        }

        [Transaction]
        public override async Task<List<TMessage>> GetMessagesAsync()
        {
            SetTransactionName(category: null, name: $"{queue.ToString()}/{nameof(GetMessagesAsync)}");

            using (LogContext.PushProperty(nameof(TMessage), typeof(TMessage).FullName))
            using (LogContext.PushProperty(nameof(IQueueClient<TMessage>), queue.ToString()))
            using (LogContext.PushProperty(nameof(IMessageHandler<TMessage>), handler.ToString()))
            {
                try
                {
                    var messages = await queue.GetMessagesAsync(options.MaxNumberOfMessages);

                    GetAgent().CurrentTransaction.AddCustomAttribute(nameof(messages.Count), messages.Count);

                    Log
                        .ForContext<QueueMessageWorker<TMessage>>()
                        .ForContext(nameof(messages.Count), messages.Count)
                        .Write(options.MessageAcquisitionLogLevel, nameof(GetMessagesAsync));

                    return messages;
                }
                catch (Exception ex)
                {
                    NoticeError(ex);

                    Log
                        .ForContext<QueueMessageWorker<TMessage>>()
                        .Error(ex, nameof(GetMessagesAsync));

                    return new List<TMessage>();
                }
            }
        }

        [Transaction]
        public override async Task ProcessMessageAsync(TMessage message)
        {
            SetTransactionName(category: null, name: $"{queue.ToString()}/{nameof(ProcessMessageAsync)}");

            using (LogContext.PushProperty(nameof(TMessage), typeof(TMessage).FullName))
            using (LogContext.PushProperty(nameof(IQueueClient<TMessage>), queue.ToString()))
            using (LogContext.PushProperty(nameof(IMessageHandler<TMessage>), handler.ToString()))
            {
                if (options.AddMessageToLogContext)
                {
                    LogContext.PushProperty(nameof(message), message, destructureObjects: true);
                }

                try
                {
                    var result = await policy.ExecuteAsync(() => handler.HandleMessageAsync(message));

                    GetAgent().CurrentTransaction.AddCustomAttribute(nameof(result.Action), result.Action.ToString());

                    using (LogContext.PushProperty(nameof(result.Action), result.Action.ToString()))
                    using (LogContext.PushProperty(nameof(result.Details), result.Details))
                    using (LogContext.PushProperty(nameof(result.Backoff), result.Backoff?.Seconds))
                    using (LogContext.PushProperty(nameof(result.Exception), result.Exception, destructureObjects: true))
                    {
                        switch (result.Action)
                        {
                            case MessageResultAction.Complete:
                                {
                                    await queue.CompleteMessageAsync(message);

                                    Log
                                        .ForContext<QueueMessageWorker<TMessage>>()
                                        .Write(options.MessageCompleteLogLevel, result.Exception, nameof(ProcessMessageAsync));

                                    break;
                                }
                            case MessageResultAction.Poison:
                                {
                                    await queue.PoisonMessageAsync(message, result.Exception, result.Details);

                                    NoticeError(result.Exception ?? new Exception(result.Details ?? nameof(ProcessMessageAsync)));

                                    Log
                                        .ForContext<QueueMessageWorker<TMessage>>()
                                        .Write(options.MessagePoisionLogLevel, result.Exception, nameof(ProcessMessageAsync));

                                    break;
                                }
                            case MessageResultAction.Retry:
                            default:
                                {
                                    await queue.RetryMessageAsync(message, result.Backoff);

                                    Log
                                        .ForContext<QueueMessageWorker<TMessage>>()
                                        .Write(options.MessageRetryLogLevel, result.Exception, nameof(ProcessMessageAsync));

                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is BrokenCircuitException)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(options.DurationOfBreakSeconds));
                    }

                    NoticeError(ex);

                    Log
                        .ForContext<QueueMessageWorker<TMessage>>()
                        .Error(ex, nameof(ProcessMessageAsync));
                }
            }
        }
    }
}
