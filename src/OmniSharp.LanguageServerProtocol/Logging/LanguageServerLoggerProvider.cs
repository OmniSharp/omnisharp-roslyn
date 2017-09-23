using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Stdio.Services;

namespace OmniSharp.LanguageServerProtocol.Logging
{
    class LanguageServerLoggerProvider : ILoggerProvider
    {
        private readonly LanguageServer _server;
        private readonly Func<string, LogLevel, bool> _filter;

        public LanguageServerLoggerProvider(LanguageServer server, Func<string, LogLevel, bool> filter)
        {
            _server = server;
            _filter = filter;
        }

        public ILogger CreateLogger(string name)
        {
            return new LanguageServerLogger(_server, name, _filter);
        }

        public void Dispose() { }
    }
}
