using System;

namespace MandalaLogics.Packing
{
    public enum StitchExceptionReason
    {
        Other = 0, OffsetOutOfBounds, CountTooLong, Collision
    }
    
    public class StitchException : Exception
    {
        public StitchExceptionReason Reason { get; }

        internal StitchException(StitchExceptionReason reason)
        {
            Reason = reason;
        }
    }
}