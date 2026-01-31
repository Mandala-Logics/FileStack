using System;

namespace MandalaLogics.Encoding
{
    public sealed class EncodingException : Exception
    {
        public EncodingException(string message) : base(message) { }
    }
}