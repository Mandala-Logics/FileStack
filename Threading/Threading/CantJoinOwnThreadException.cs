using System;

namespace MandalaLogics.Threading
{
    public sealed class CantJoinOwnThreadException : Exception
    {
        public CantJoinOwnThreadException() : base() { }
    }
}