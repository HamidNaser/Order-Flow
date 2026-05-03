using System.Threading;
using System.Threading.Tasks;

namespace Order.MessagePump.Locks
{
    public interface ILockManager
    {
        Task<AcquireLockResponse> AcquireLockAsync(AcquireLockRequest request, CancellationToken cancellationToken = default);

        Task<ReleaseLockResponse> ReleaseLockAsync(ReleaseLockRequest request, CancellationToken cancellationToken = default);
    }
}
