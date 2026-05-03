using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Order.MessagePump.Locks;
using OrderHub.Common.Configuration.Locks;
using OrderHub.Common.Services;
using Xunit;

namespace OrderHub.UnitTests.Services;

public class CustomerLockServiceTests
{
    private readonly ILockManager _lockManager = Substitute.For<ILockManager>();
    private readonly CustomerLockService _sut;

    public CustomerLockServiceTests()
    {
        var options = Options.Create(new LockingOptions { TtlSeconds = 30 });
        _sut = new CustomerLockService(_lockManager, options);
    }

    // ──────────────────────────────────────────────
    // AcquireLocksAsync — success paths
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AcquireLocksAsync_SingleCustomerId_AcquiresLock()
    {
        // Arrange
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcquireLockResponse { IsLockAcquired = true, LockData = new() });

        // Act
        var lease = await _sut.AcquireLocksAsync(["customer-1"]);

        // Assert
        Assert.True(lease.IsAcquired);
        await _lockManager.Received(1).AcquireLockAsync(
            Arg.Is<AcquireLockRequest>(r => r.LockId == "ccid:customer-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireLocksAsync_MultipleCustomerIds_AcquiresInLexicographicOrder()
    {
        // Arrange
        var callOrder = new List<string>();
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callOrder.Add(callInfo.Arg<AcquireLockRequest>().LockId);
                return new AcquireLockResponse { IsLockAcquired = true, LockData = new() };
            });

        // Act — pass in reverse order to verify sorting
        var lease = await _sut.AcquireLocksAsync(["charlie", "alpha", "bravo"]);

        // Assert
        Assert.True(lease.IsAcquired);
        Assert.Equal(["ccid:alpha", "ccid:bravo", "ccid:charlie"], callOrder);
    }

    // ──────────────────────────────────────────────
    // AcquireLocksAsync — input normalization
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AcquireLocksAsync_DeduplicatesCaseInsensitive()
    {
        // Arrange
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcquireLockResponse { IsLockAcquired = true, LockData = new() });

        // Act
        var lease = await _sut.AcquireLocksAsync(["Customer-A", "customer-a", "CUSTOMER-A"]);

        // Assert
        Assert.True(lease.IsAcquired);
        await _lockManager.Received(1).AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireLocksAsync_TrimsWhitespace()
    {
        // Arrange
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcquireLockResponse { IsLockAcquired = true, LockData = new() });

        // Act
        var lease = await _sut.AcquireLocksAsync(["  customer-1  "]);

        // Assert
        Assert.True(lease.IsAcquired);
        await _lockManager.Received(1).AcquireLockAsync(
            Arg.Is<AcquireLockRequest>(r => r.LockId == "ccid:customer-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireLocksAsync_SkipsNullAndWhitespace()
    {
        // Arrange & Act
        var lease = await _sut.AcquireLocksAsync([null!, "", "  "]);

        // Assert
        Assert.False(lease.IsAcquired);
        await _lockManager.DidNotReceiveWithAnyArgs().AcquireLockAsync(default!, default);
    }

    [Fact]
    public async Task AcquireLocksAsync_EmptyList_ReturnsNotAcquired()
    {
        // Arrange & Act
        var lease = await _sut.AcquireLocksAsync([]);

        // Assert
        Assert.False(lease.IsAcquired);
    }

    // ──────────────────────────────────────────────
    // AcquireLocksAsync — rollback on failure
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AcquireLocksAsync_SecondLockFails_ReleasesFirstLock()
    {
        // Arrange
        var firstLockData = new Dictionary<string, object> { ["token"] = "abc" };

        var callCount = 0;
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 1)
                    return new AcquireLockResponse { IsLockAcquired = true, LockData = firstLockData };
                return new AcquireLockResponse { IsLockAcquired = false };
            });

        _lockManager.ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseLockResponse { WasReleased = true });

        // Act
        var lease = await _sut.AcquireLocksAsync(["alpha", "bravo"]);

        // Assert
        Assert.False(lease.IsAcquired);
        await _lockManager.Received(1).ReleaseLockAsync(
            Arg.Is<ReleaseLockRequest>(r => r.LockData == firstLockData),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireLocksAsync_LockManagerThrows_ReleasesAcquiredAndReturnsNotAcquired()
    {
        // Arrange
        var firstLockData = new Dictionary<string, object> { ["token"] = "xyz" };

        var callCount = 0;
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 1)
                    return new AcquireLockResponse { IsLockAcquired = true, LockData = firstLockData };
                throw new InvalidOperationException("lock service down");
            });

        _lockManager.ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseLockResponse { WasReleased = true });

        // Act
        var lease = await _sut.AcquireLocksAsync(["alpha", "bravo"]);

        // Assert
        Assert.False(lease.IsAcquired);
        await _lockManager.Received(1).ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    // ReleaseLocksAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReleaseLocksAsync_NotAcquiredLease_ReturnsTrueWithoutCallingLockManager()
    {
        // Arrange
        var lease = Substitute.For<ICustomerLockLease>();
        lease.IsAcquired.Returns(false);

        // Act
        var result = await _sut.ReleaseLocksAsync(lease);

        // Assert
        Assert.True(result);
        await _lockManager.DidNotReceiveWithAnyArgs().ReleaseLockAsync(default!, default);
    }

    [Fact]
    public async Task ReleaseLocksAsync_NullLease_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ReleaseLocksAsync(null!));
    }

    [Fact]
    public async Task ReleaseLocksAsync_AcquiredLease_ReleasesAllLocks()
    {
        // Arrange — acquire a real lease first
        var lockData1 = new Dictionary<string, object> { ["token"] = "t1" };
        var lockData2 = new Dictionary<string, object> { ["token"] = "t2" };

        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new AcquireLockResponse { IsLockAcquired = true, LockData = lockData1 },
                new AcquireLockResponse { IsLockAcquired = true, LockData = lockData2 }
            );

        _lockManager.ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseLockResponse { WasReleased = true });

        var lease = await _sut.AcquireLocksAsync(["alpha", "bravo"]);
        Assert.True(lease.IsAcquired);

        // Act
        var result = await _sut.ReleaseLocksAsync(lease);

        // Assert
        Assert.True(result);
        await _lockManager.Received(2).ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReleaseLocksAsync_CalledTwice_SecondCallIsIdempotent()
    {
        // Arrange
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcquireLockResponse { IsLockAcquired = true, LockData = new() });

        _lockManager.ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseLockResponse { WasReleased = true });

        var lease = await _sut.AcquireLocksAsync(["customer-1"]);

        // Act
        var firstRelease = await _sut.ReleaseLocksAsync(lease);
        var secondRelease = await _sut.ReleaseLocksAsync(lease);

        // Assert
        Assert.True(firstRelease);
        Assert.True(secondRelease);
        // Only one actual release call to the lock manager
        await _lockManager.Received(1).ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReleaseLocksAsync_OneReleaseFails_ReturnsFalse()
    {
        // Arrange
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new AcquireLockResponse { IsLockAcquired = true, LockData = new() },
                new AcquireLockResponse { IsLockAcquired = true, LockData = new() }
            );

        _lockManager.ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ReleaseLockResponse { WasReleased = true },
                new ReleaseLockResponse { WasReleased = false }
            );

        var lease = await _sut.AcquireLocksAsync(["alpha", "bravo"]);

        // Act
        var result = await _sut.ReleaseLocksAsync(lease);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseLocksAsync_ReleaseThrows_ReturnsFalse()
    {
        // Arrange
        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcquireLockResponse { IsLockAcquired = true, LockData = new() });

        _lockManager.ReleaseLockAsync(Arg.Any<ReleaseLockRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var lease = await _sut.AcquireLocksAsync(["customer-1"]);

        // Act
        var result = await _sut.ReleaseLocksAsync(lease);

        // Assert
        Assert.False(result);
    }

    // ──────────────────────────────────────────────
    // Constructor — TtlSeconds edge cases
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Constructor_InvalidTtl_DefaultsTo60Seconds(int ttlSeconds)
    {
        // Arrange
        var options = Options.Create(new LockingOptions { TtlSeconds = ttlSeconds });
        var sut = new CustomerLockService(_lockManager, options);

        _lockManager.AcquireLockAsync(Arg.Any<AcquireLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcquireLockResponse { IsLockAcquired = true, LockData = new() });

        // Act
        await sut.AcquireLocksAsync(["customer-1"]);

        // Assert
        await _lockManager.Received(1).AcquireLockAsync(
            Arg.Is<AcquireLockRequest>(r => r.LockDuration == TimeSpan.FromSeconds(60)),
            Arg.Any<CancellationToken>());
    }
}
