using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Stdio.Services;

namespace OmniSharp.LanguageServerProtocol.Logging
{
    static class LanguageServerLoggerExtensions
    {
        // public static ILoggerFactory AddLanguageServer(this ILoggerFactory factory, LanguageServer server, Func<string, LogLevel, bool> filter)
        // {
        //     factory.AddProvider(new LanguageServerLoggerProvider(server, filter));
        //     return factory;
        // }
    }
}
