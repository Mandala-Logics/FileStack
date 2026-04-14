using System;

namespace MandalaLogics.Database
{
    public sealed class DatabaseException : Exception
    {
        public DatabaseException(string message) : base(message) { }
    }
}