using System;

namespace MandalaLogics
{
    public sealed class ProgrammerException : Exception
    {
        public ProgrammerException(string message) : base(message) { }
        public ProgrammerException(string message, Exception inner) : base(message, inner) { }
    }
}