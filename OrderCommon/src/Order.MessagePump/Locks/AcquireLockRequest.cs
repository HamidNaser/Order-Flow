using System;

namespace Order.MessagePump.Locks
{
    public class AcquireLockRequest
    {
        public string LockId { get; set; } = string.Empty;

        public TimeSpan LockDuration { get; set; }
    }
}
