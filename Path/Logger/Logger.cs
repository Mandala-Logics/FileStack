using System;

public enum LogLevel : int
{
    Unknown = 0, Warning = 2, Important = 3, Critical = 5, Fatal = 6,
    Verbose = 1
}

namespace MandalaLogics.Logging
{
    public class Logger
    {
        public static readonly Logger Null = new Logger(LogLevel.Unknown);
        
        public LogLevel Level {get; set;}

        public Logger(LogLevel level = LogLevel.Warning)
        {
            Level = level;
        }

        public void LogException(Exception e, LogLevel level)
        {
            if (level < Level) { return; }

            Console.Error.WriteLine($"EXCEPTION[{level}]:".ToUpper());
            Console.Error.WriteLine(e.FormatException());

            if (e.InnerException is Exception inner)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"INNER EXCEPTION:".ToUpper());
                Console.Error.WriteLine(inner.FormatException());
            }

            AfterLogException(e, level);
        }

        public void LogMessage(string message, LogLevel level, bool asError = false)
        {
            if (level < Level) { return; }

            if (asError) { Console.Error.WriteLine($"MESSAGE[{level}]: ".ToUpper() + message); }
            else { Console.WriteLine($"MESSAGE[{level}]: ".ToUpper() + message); }

            AfterLogMessage(message, level);
        }

        protected virtual void AfterLogException(Exception e, LogLevel level) { }
        protected virtual void AfterLogMessage(string message, LogLevel level) { }
    }
}
