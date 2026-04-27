namespace OrderHub.Common.Services;

public interface ICustomerLockService
{
    Task<ICustomerLockLease> AcquireLocksAsync(IEnumerable<string> customerIds);

    Task<bool> ReleaseLocksAsync(ICustomerLockLease lease);
}

public interface ICustomerLockLease
{
    bool IsAcquired { get; }

    /// <summary>
    /// Attempts to mark this lease as released. Used to make release idempotent without the lease
    /// performing the release itself.
    /// </summary>
    bool MarkReleased();
}
