using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.LanguageServerProtocol.Logging;
using OmniSharp.Services;

namespace OmniSharp.LanguageServerProtocol
{
    class LanguageServerLoggerFactory : ILoggerFactory
    {
        private readonly LanguageServerLoggerProvider _provider;

        public LanguageServerLoggerFactory()
        {
            _provider = new LanguageServerLoggerProvider();
        }
        public void AddProvider(ILoggerProvider provider) { }
        public void AddProvider(LanguageServer server, OmniSharpEnvironment environment)
        {
            if (environment.LogLevel <= LogLevel.Debug)
                _provider.SetProvider(server, (category, level) => true);
            else
                _provider.SetProvider(server, (category, level) => HostHelpers.LogFilter(category, level, environment));
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _provider?.CreateLogger(categoryName) ?? NullLogger.Instance;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
