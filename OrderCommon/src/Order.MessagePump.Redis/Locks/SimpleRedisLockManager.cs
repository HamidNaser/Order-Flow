using Order.MessagePump.Locks;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.MessagePump.Redis.Locks
{
    public class SimpleRedisLockManager : ILockManager
    {
        private readonly Lazy<Task<IConnectionMultiplexer>> _lazyConnection;

        /// <summary>
        /// Creates a lock manager with deferred async connection.
        /// Connection is established on first lock operation, avoiding sync-over-async in the constructor.
        /// </summary>
        public SimpleRedisLockManager(string connectionString)
        {
            _lazyConnection = new Lazy<Task<IConnectionMultiplexer>>(
                () => ConnectionMultiplexer.ConnectAsync(connectionString).ContinueWith<IConnectionMultiplexer>(t => t.Result));
        }

        public SimpleRedisLockManager(IConnectionMultiplexer connection)
        {
            _lazyConnection = new Lazy<Task<IConnectionMultiplexer>>(
                () => Task.FromResult(connection));
        }

        private async Task<IDatabase> GetDatabaseAsync()
        {
            var connection = await _lazyConnection.Value.ConfigureAwait(false);
            return connection.GetDatabase();
        }

        public async Task<AcquireLockResponse> AcquireLockAsync(AcquireLockRequest request, CancellationToken cancellationToken = default)
        {
            var lockReceipt = Guid.NewGuid().ToString();

            var db = await GetDatabaseAsync();
            var result = await db
                .StringSetAsync(
                    $"{nameof(SimpleRedisLockManager)}-{request.LockId}",
                    lockReceipt,
                    request.LockDuration,
                    When.NotExists,
                    CommandFlags.DemandMaster);

            return new AcquireLockResponse
            {
                IsLockAcquired = result,
                LockData = new Dictionary<string, object>
                {
                    ["LockReceipt"] = lockReceipt,
                    ["LockId"] = request.LockId,
                    ["ExpiresUtc"] = DateTime.UtcNow.Add(request.LockDuration)
                }
            };
        }

        public async Task<ReleaseLockResponse> ReleaseLockAsync(ReleaseLockRequest request, CancellationToken cancellationToken = default)
        {
            var lockData = request?.LockData ?? new Dictionary<string, object> { };
            var lockReceipt = lockData.TryGetValue("LockReceipt", out var r) ? r.ToString() : default;
            var lockId = lockData.TryGetValue("LockId", out var i) ? i.ToString() : default;

            if (string.IsNullOrWhiteSpace(lockReceipt) || string.IsNullOrWhiteSpace(lockId))
            {
                return new ReleaseLockResponse { WasReleased = false };
            }

            var db = await GetDatabaseAsync();
            var result = (bool)await db
                .ScriptEvaluateAsync(
                    "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                    new RedisKey[] { $"{nameof(SimpleRedisLockManager)}-{lockId}" },
                    new RedisValue[] { lockReceipt },
                    CommandFlags.DemandMaster);

            return new ReleaseLockResponse { WasReleased = result };
        }
    }
}
