using System;

namespace MandalaLogics.Splice
{
    public class SpliceNotValidException : Exception
    {
        internal SpliceNotValidException(string message, Exception inner) : base(message, inner) {}
        
        internal SpliceNotValidException(string message) : base(message) {}
    }
}