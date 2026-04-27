using Order.MessagePump.Locks;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.MessagePump.Redis.Locks
{
    public class SimpleRedisLockManager : ILockManager
    {
        private readonly IConnectionMultiplexer connection;

        public SimpleRedisLockManager(string connectionString)
        {
            connection = ConnectionMultiplexer.Connect(connectionString);
        }

        public SimpleRedisLockManager(IConnectionMultiplexer connection)
        {
            this.connection = connection;
        }

        public async Task<AcquireLockResponse> AcquireLockAsync(AcquireLockRequest request)
        {
            var lockReceipt = Guid.NewGuid().ToString();

            var result = await connection
                .GetDatabase()
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

        public async Task<ReleaseLockResponse> ReleaseLockAsync(ReleaseLockRequest request)
        {
            var lockData = request?.LockData ?? new Dictionary<string, object> { };
            var lockReceipt = lockData.TryGetValue("LockReceipt", out var r) ? r.ToString() : default;
            var lockId = lockData.TryGetValue("LockId", out var i) ? i.ToString() : default;

            if (string.IsNullOrWhiteSpace(lockReceipt) || string.IsNullOrWhiteSpace(lockId))
            {
                return new ReleaseLockResponse { WasReleased = false };
            }

            var result = (bool)await connection
                .GetDatabase()
                .ScriptEvaluateAsync(
                    "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                    new RedisKey[] { $"{nameof(SimpleRedisLockManager)}-{lockId}" },
                    new RedisValue[] { lockReceipt },
                    CommandFlags.DemandMaster);

            return new ReleaseLockResponse { WasReleased = result };
        }
    }
}
