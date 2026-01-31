using System;

namespace MandalaLogics.Database
{
    public class HeaderFileNotValidException : Exception
    {
        public HeaderFileNotValidException(string message, Exception innerException) : base(message, innerException) {}
        
        public HeaderFileNotValidException(string message) : base(message) {}
    }
}