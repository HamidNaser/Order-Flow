namespace Order.MessagePump.Pipelines
{
    public class PipelineWorkerOptions
    {
        public int BackoffSeconds { get; set; } = 60;
    }
}
