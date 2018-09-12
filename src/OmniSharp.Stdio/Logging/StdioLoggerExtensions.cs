using Microsoft.Extensions.Logging;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio.Logging
{
    static class StdioLoggerExtensions
    {
        public static ILoggingBuilder AddStdio(this ILoggingBuilder builder, ISharedTextWriter writer)
        {
            builder.AddProvider(new StdioLoggerProvider(writer));
            return builder;
        }
    }
}
