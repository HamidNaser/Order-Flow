using System.Collections.Generic;

namespace Order.MessagePump.Locks
{
    public class AcquireLockResponse
    {
        public bool IsLockAcquired { get; set; }

        public Dictionary<string, object>? LockData { get; set; }
    }
}
