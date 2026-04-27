using System.Collections.Generic;

namespace Order.MessagePump.Locks
{
    public class ReleaseLockRequest
    {
        public Dictionary<string, object>? LockData { get; set; }
    }
}
