using System;

namespace MandalaLogics.Streams
{
    public sealed class WrongTaskTypeException : Exception
    {
        public WrongTaskTypeException(string message) : base(message) { }
    }
}