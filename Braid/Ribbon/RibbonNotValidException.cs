using System;

namespace MandalaLogics.Packing
{
    public class RibbonNotValidException : Exception
    {
        public RibbonNotValidException() {}
        public RibbonNotValidException(string message, Exception innerException) : base(message, innerException) {}
        public RibbonNotValidException(string message) : base(message) {}
    }
}