namespace OrderGateway.Common.Models
{
    public sealed class DisposableList : List<IDisposable>, IDisposable
    {
        public void Dispose()
        {
            foreach (var disposable in this)
            {
                disposable.Dispose();
            }
        }
    }
}
