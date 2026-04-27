using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Order.MessagePump.Pipelines
{
    public abstract class MessagePipelineWorkerBase<TMessage> : PipelineWorkerBase<List<TMessage>> where TMessage : class
    {
        private readonly MessagePipelineWorkerOptions options;

        public MessagePipelineWorkerBase(MessagePipelineWorkerOptions options)
            : base(options)
        {
            this.options = options;
        }

        public abstract Task<List<TMessage>> GetMessagesAsync();

        public abstract Task ProcessMessageAsync(TMessage message);

        protected override Pipeline<List<TMessage>> CreatePipeline(CancellationToken cancellationToken)
        {
            ExecutionDataflowBlockOptions GetExecutionOptions(int parallelism) => new ExecutionDataflowBlockOptions()
            {
                CancellationToken = cancellationToken,
                BoundedCapacity = parallelism,
                MaxDegreeOfParallelism = parallelism
            };

            var flatten = new TransformManyBlock<List<TMessage>, TMessage>(records => records, GetExecutionOptions(1));

            var process = new ActionBlock<TMessage>(ProcessMessageAsync, GetExecutionOptions(options.ProcessParallelism));

            var dataflowLinkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            flatten.LinkTo(process, dataflowLinkOptions);

            return new Pipeline<List<TMessage>>(flatten, process);
        }

        protected override async Task<List<TMessage>?> GetNextItemAsync()
        {
            var messages = await GetMessagesAsync();

            return messages.Count > 0 ? messages : null; // trigger the backoff
        }
    }
}
