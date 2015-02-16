using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Stdio.Logging
{
    public static class StdioLoggerExtensions
    {
        public static ILoggerFactory AddStdio(this ILoggerFactory factory, Func<string, LogLevel, bool> filter)
        {
            factory.AddProvider(new StdioLoggerProvider(filter));
            return factory;
        }
    }
}
