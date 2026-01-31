using System;

namespace MandalaLogics.Stacking
{
    public class StackNotValidException : Exception
    {
        public StackNotValidException() {}
        public StackNotValidException(string message, Exception innerException) : base(message, innerException) {}
        
        public StackNotValidException(string message) : base(message) {}
    }
}