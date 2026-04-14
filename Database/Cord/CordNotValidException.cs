using System;

namespace MandalaLogics.Database
{
    public class CordNotValidException : Exception
    {
        internal CordNotValidException(string message, Exception inner) : base(message, inner) {}
        
        internal CordNotValidException(string message) : base(message) {}
    }
}