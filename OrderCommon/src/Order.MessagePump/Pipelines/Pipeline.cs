using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Order.MessagePump.Pipelines
{
    public class Pipeline<T> : IDisposable, IAsyncDisposable
    {
        private readonly ITargetBlock<T> startBlock;
        private readonly IDataflowBlock finalBlock;

        public Pipeline(ITargetBlock<T> singleBlock) : this(singleBlock, singleBlock)
        {
        }

        public Pipeline(ITargetBlock<T> startBlock, IDataflowBlock finalBlock)
        {
            this.startBlock = startBlock;
            this.finalBlock = finalBlock;
        }

        public async Task SendAsync(T item)
        {
            await startBlock.SendAsync(item);
        }

        public async Task FlushAsync()
        {
            startBlock.Complete();

            await finalBlock.Completion;
        }

        private bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                // Signal completion without blocking. Prefer 'await using' with DisposeAsync
                // for safe flushing — sync Dispose cannot safely wait for async completion
                // without risking deadlocks (sync-over-async).
                startBlock.Complete();
            }

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                await FlushAsync().ConfigureAwait(false);
                disposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
