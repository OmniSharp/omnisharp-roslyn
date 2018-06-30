using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Stdio.Services;

namespace OmniSharp.LanguageServerProtocol.Logging
{
    class LanguageServerLoggerProvider : ILoggerProvider
    {
        internal ILanguageServer _server { get; private set; }
        internal Func<string, LogLevel, bool> _filter { get; private set; }

        public LanguageServerLoggerProvider()        {        }
        public void SetProvider(ILanguageServer server, Func<string, LogLevel, bool> filter)
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
