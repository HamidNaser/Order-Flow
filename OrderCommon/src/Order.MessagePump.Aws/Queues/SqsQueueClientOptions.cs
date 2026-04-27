namespace Order.MessagePump.Aws.Queues
{
    public class SqsQueueClientOptions
    {
        public string? QueueName { get; set; }

        public string? QueueUrl { get; set; }

        public string? PoisonQueueName { get; set; }

        public string? PoisonQueueUrl { get; set; }

        public int WaitTimeSeconds { get; set; } = 1;

        public string Region { get; set; } = "us-east-1";

        public string? ServiceURL { get; set; }
    }
}
