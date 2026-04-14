using System;

namespace MandalaLogics.Database
{
    public class TwineNotValidException : Exception
    {
        internal TwineNotValidException(string message, Exception inner) : base(message, inner) {}
        
        internal TwineNotValidException(string message) : base(message) {}
    }
}