using Polly;

namespace OrderGateway.IntegrationTests
{
    public static class RetryHelpers
    {
        public static void UntilSuccess(Action action, int attempts = 20, int delaySeconds = 3)
        {
            UntilSuccess<Exception>(action, attempts, delaySeconds);
        }

        public static void UntilSuccess<TException>(Action action, int attempts = 20, int delaySeconds = 3)
            where TException : Exception
        {
            Policy
                .Handle<TException>()
                .WaitAndRetry(attempts, _ => TimeSpan.FromSeconds(delaySeconds))
                .Execute(action.Invoke);
        }

        public static Task UntilSuccessAsync(Func<Task> action, int attempts = 20, int delaySeconds = 3)
        {
            return UntilSuccessAsync<Exception>(action, attempts, delaySeconds);
        }

        public static Task UntilSuccessAsync<TException>(Func<Task> action, int attempts = 20, int delaySeconds = 3)
            where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetryAsync(attempts, _ => TimeSpan.FromSeconds(delaySeconds))
                .ExecuteAsync(action.Invoke);
        }

        public static void WhileSuccess(Action action, int attempts = 20, int delaySeconds = 3)
        {
            for (int i = 0; i <= attempts; i++)
            {
                action.Invoke();
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }
}
