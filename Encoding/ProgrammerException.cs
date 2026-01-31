using System;

namespace MandalaLogics.Encoding
{
    public sealed class ProgrammerException : Exception
    {
        public ProgrammerException(string message) : base(message) { }
        public ProgrammerException(string message, Exception inner) : base(message, inner) { }
    }
}