using System.Threading.Tasks;

namespace Order.MessagePump.Locks
{
    public interface ILockManager
    {
        Task<AcquireLockResponse> AcquireLockAsync(AcquireLockRequest request);

        Task<ReleaseLockResponse> ReleaseLockAsync(ReleaseLockRequest request);
    }
}
