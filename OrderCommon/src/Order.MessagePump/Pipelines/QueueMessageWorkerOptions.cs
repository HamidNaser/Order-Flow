using Serilog.Events;

namespace Order.MessagePump.Pipelines
{
    public class QueueMessageWorkerOptions : MessagePipelineWorkerOptions
    {
        public int MaxNumberOfMessages { get; set; } = 10;

        public int ExceptionsAllowedBeforeBreaking { get; set; } = 5;

        public int DurationOfBreakSeconds { get; set; } = 5 * 60;

        public bool AddMessageToLogContext { get; set; } = true;

        public LogEventLevel MessageAcquisitionLogLevel { get; set; } = LogEventLevel.Debug;

        public LogEventLevel MessageCompleteLogLevel { get; set; } = LogEventLevel.Debug;

        public LogEventLevel MessagePoisionLogLevel { get; set; } = LogEventLevel.Error;

        public LogEventLevel MessageRetryLogLevel { get; set; } = LogEventLevel.Warning;
    }
}
