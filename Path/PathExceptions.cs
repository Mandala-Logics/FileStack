using System;
using System.IO;

namespace MandalaLogics.Path
{
    public enum IOHResultValues : int
    {
        Success = 0,
        FileSharingViolation = 32, //The file name is missing, or the file or directory is in use.
        FileAlreadyExists = 80, //The file already exists.
        InvalidParameter = 87, //An argument supplied to the method is invalid.
        FileOrDirectoryAlreadyExists = 183 //The file or directory already exists.
    }

    public sealed class NameNotValidException : PathException
    {
        public NameNotValidException(string message) : base(message) { }
    }

    public sealed class PathImplimentationException : PathException
    {
        public PathImplimentationException(string message) : base(message) { }
        public PathImplimentationException(string message, Exception innerException) : base(message, innerException) { }
    }
    public sealed class PathParsingException : PathException
    {
        public PathParsingException(string message) : base(message) { }
    }
    public sealed class PathAccessException : PathException
    {
        public PathBase Path { get; }
        public IOHResultValues IOErrorCode => (IOHResultValues)HResult;

        public PathAccessException(string message) : base(message) { }
        public PathAccessException(PathBase path, string message) : base(message) { Path = path; }
        public PathAccessException(PathBase path, string message, Exception innerException) : base(message, innerException) { Path = path; }
        public PathAccessException(PathBase path, string message, IOException innerException) : base(message, innerException)
        {
            HResult = innerException.HResult;
            Path = path;
        }
    }
    public sealed class PathTypeException : PathException
    {
        public PathTypeException(string message) : base(message) { }
    }
    public class PathException : Exception
    {
        public PathException(string message) : base(message) { }
        public PathException(string message, Exception innerException) : base(message, innerException) { }
    }
}
