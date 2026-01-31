using System;

namespace MandalaLogics.Packing
{
    public class BraidNotValidException : Exception
    {
        public BraidNotValidException() {}
        public BraidNotValidException(string message, Exception innerException) : base(message, innerException) {}
        
        public BraidNotValidException(string message) : base(message) {}
    }
}