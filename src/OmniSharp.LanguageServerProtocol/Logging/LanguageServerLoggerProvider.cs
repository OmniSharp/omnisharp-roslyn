using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Stdio.Services;

namespace OmniSharp.LanguageServerProtocol.Logging
{
    class LanguageServerLoggerProvider : ILoggerProvider
    {
        internal LanguageServer _server { get; private set; }
        internal Func<string, LogLevel, bool> _filter { get; private set; }

        public LanguageServerLoggerProvider()        {        }
        public void SetProvider(LanguageServer server, Func<string, LogLevel, bool> filter)
        {
            _server = server;
            _filter = filter;
        }

        public ILogger CreateLogger(string name)
        {
            return new LanguageServerLogger(this, name);
        }
        public void Dispose() { }
    }
}
