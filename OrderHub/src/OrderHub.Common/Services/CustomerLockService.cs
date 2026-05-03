using OrderHub.Common.Configuration.Locks;
using Order.MessagePump.Locks;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

namespace OrderHub.Common.Services;

/// <summary>
/// Customer-specific lock wrapper over <see cref="ILockManager"/>.
/// Acquires locks in deterministic, case-insensitive lexicographic order and returns a per-acquisition
/// lease object that owns release responsibilities.
/// </summary>
public class CustomerLockService : ICustomerLockService
{
    private readonly ILockManager _lockManager;
    private readonly TimeSpan _lockDuration;

    public CustomerLockService(ILockManager lockManager, IOptions<LockingOptions> lockingOptions)
    {
        _lockManager = lockManager;
        var ttlSeconds = lockingOptions?.Value?.TtlSeconds ?? 60;
        _lockDuration = TimeSpan.FromSeconds(ttlSeconds > 0 ? ttlSeconds : 60);
    }

    /// <summary>
    /// Acquires locks for one or many customerIds in deterministic, case-insensitive lexicographic order.
    /// Inputs are normalized by trimming, ignoring null/whitespace values, and de-duplicating case-insensitively.
    /// On any acquisition failure (including exceptions from the lock manager), releases any locks already
    /// acquired in this call and returns a non-acquired lease.
    /// </summary>
    /// <param name="customerIds">Customer identifiers to lock.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A lease representing the acquired locks; if no locks are acquired, returns a non-acquired lease.
    /// </returns>
    /// <remarks>
    /// This method is best-effort and will not throw for lock-manager acquisition failures; however, it may throw
    /// if <paramref name="customerIds"/> enumeration or LINQ processing throws.
    /// </remarks>
    public async Task<ICustomerLockLease> AcquireLocksAsync(IEnumerable<string> customerIds, CancellationToken cancellationToken = default)
    {
        var orderedIds = customerIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedIds == null || orderedIds.Count == 0)
        {
            return CustomerLockLease.NotAcquired;
        }

        var lockResponses = new List<AcquireLockResponse>();

        foreach (var customerId in orderedIds)
        {
            AcquireLockResponse? response;

            try
            {
                response = await _lockManager.AcquireLockAsync(new AcquireLockRequest
                {
                    LockId = BuildLockId(customerId),
                    LockDuration = _lockDuration
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Log
                    .ForContext<CustomerLockService>()
                    .Warning(ex, "Exception acquiring lock for customerId {CustomerId}. Counting as a failure.", customerId);

                response = null;
            }

            if (response?.IsLockAcquired == true)
            {
                lockResponses.Add(response);
                continue;
            }

            Log
                .ForContext<CustomerLockService>()
                .Warning(
                    "Customer lock acquisition failure. Acquired {AcquiredCount}/{RequestedCount} locks; failed on customerId {CustomerId}.",
                    lockResponses.Count,
                    orderedIds.Count,
                    customerId);

            // A lock acquisition failed; release any previously acquired locks.
            await ReleaseLocksAsync(new CustomerLockLease(lockResponses));
            return CustomerLockLease.NotAcquired;
        }

        return new CustomerLockLease(lockResponses);
    }

    /// <summary>
    /// Releases all locks owned by the provided <paramref name="lease"/>.
    /// This method is safe to call multiple times for the same lease (idempotent);
    /// subsequent calls after the first successful mark will no-op.
    /// </summary>
    /// <param name="lease">The lease returned from <see cref="AcquireLocksAsync"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the lease is not acquired, has already been released, or when all underlying
    /// lock releases succeed; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lease"/> is <see langword="null"/>.</exception>
    public async Task<bool> ReleaseLocksAsync(ICustomerLockLease lease)
    {

        if (lease == null)
        {
            throw new ArgumentNullException(nameof(lease));
        }

        if (!lease.IsAcquired)
        {
            lease.MarkReleased();
            return true;
        }

        if (!lease.MarkReleased())
        {
            return true;
        }

        if (lease is not CustomerLockLease customerLease || customerLease.LockResponses.Count == 0)
        {
            return true;
        }
        using (LogContext.PushProperty("LocksToRelease", customerLease.LockResponses, destructureObjects: true))
        {
            var allReleased = true;
            var releasedCount = 0;

            foreach (var response in customerLease.LockResponses)
            {
                try
                {
                    var releaseResult = await _lockManager.ReleaseLockAsync(new ReleaseLockRequest
                    {
                        LockData = response.LockData
                    });

                    var wasReleased = releaseResult?.WasReleased ?? false;
                    allReleased &= wasReleased;
                    if (wasReleased)
                    {
                        releasedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log
                        .ForContext<CustomerLockService>()
                        .Error(ex, "Exception releasing lock for Lock {@Lock}. Counting as a failure.", response.LockData);

                    allReleased = false;
                }
            }

            if (!allReleased)
            {
                Log
                    .ForContext<CustomerLockService>()
                    .Error(
                        "Failed to release one or more customer locks. Released {ReleasedCount}/{TotalCount} locks.",
                        releasedCount,
                        customerLease.LockResponses.Count);
            }

            return allReleased;
        }
    }

    private static string BuildLockId(string customerId) => $"ccid:{customerId}";

    private sealed class CustomerLockLease(List<AcquireLockResponse> lockResponses)
        : ICustomerLockLease
    {
        public static readonly CustomerLockLease NotAcquired = new(lockResponses: []);

        internal List<AcquireLockResponse> LockResponses { get; } = lockResponses;

        private int _released;

        public bool IsAcquired => LockResponses.Count > 0;

        public bool MarkReleased()
        {
            return Interlocked.Exchange(ref _released, 1) == 0;
        }
    }
}
