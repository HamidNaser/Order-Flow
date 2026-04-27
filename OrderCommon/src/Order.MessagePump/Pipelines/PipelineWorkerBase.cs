using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Order.MessagePump.Pipelines
{
    public abstract class PipelineWorkerBase<T> : BackgroundService where T : class
    {
        private readonly PipelineWorkerOptions options;

        public PipelineWorkerBase(PipelineWorkerOptions options)
        {
            this.options = options;
        }

        protected abstract Pipeline<T> CreatePipeline(CancellationToken cancellationToken);

        protected abstract Task<T?> GetNextItemAsync();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (options.BackoffSeconds <= 0)
            {
                return; // disables the pipeline
            }

            await using (var pipeline = CreatePipeline(stoppingToken))
            {
                while (stoppingToken.IsCancellationRequested == false)
                {
                    T? nextItem = null;

                    try
                    {
                        nextItem = await GetNextItemAsync();
                    }
                    catch (Exception ex)
                    {
                        Log
                            .ForContext<PipelineWorkerBase<T>>()
                            .Error(ex, nameof(PipelineWorkerBase<T>));
                    }

                    if (nextItem == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(options.BackoffSeconds));
                    }
                    else
                    {
                        await pipeline.SendAsync(nextItem);
                    }
                }

                await pipeline.FlushAsync();
            }
        }
    }
}
