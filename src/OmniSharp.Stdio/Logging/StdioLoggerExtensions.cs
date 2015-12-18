using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio.Logging
{
    public static class StdioLoggerExtensions
    {
        public static ILoggerFactory AddStdio(this ILoggerFactory factory, ISharedTextWriter writer, Func<string, LogLevel, bool> filter)
        {
            factory.AddProvider(new StdioLoggerProvider(writer, filter));
            return factory;
        }
    }
}
