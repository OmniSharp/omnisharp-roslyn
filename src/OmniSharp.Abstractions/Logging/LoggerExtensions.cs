using System;

namespace Microsoft.Extensions.Logging
{
    public static class LoggerExceptions
    {
        public static void LogError(this ILogger logger, Exception ex, string message, params object[] args)
        {
            logger.LogError(0, ex, message, args);
        }

        public static void LogTrace(this ILogger logger, Exception ex, string message, params object[] args)
        {
            logger.LogTrace(0, ex, message, args);
        }
    }
}
