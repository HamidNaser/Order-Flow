namespace Order.MessagePump.Pipelines
{
    public class MessagePipelineWorkerOptions : PipelineWorkerOptions
    {
        public int ProcessParallelism { get; set; } = 1;
    }
}
