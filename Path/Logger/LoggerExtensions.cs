using System;
using System.Text;

namespace MandalaLogics.Logging
{
    public static class LoggerExtensions
    {
        public static Exception Log(this Exception e, Logger logger, LogLevel level)
        {
            logger.LogException(e, level);

            return e;
        }

        public static string FormatException(this Exception e)
        {
            StringBuilder sb = new StringBuilder(e.GetType().Name);

            sb.Append($"\n\n{e.Message}\n\n");

            sb.Append(e.StackTrace);

            return sb.ToString();
        }

    }
}
