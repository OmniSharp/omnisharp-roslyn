using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.LanguageServerProtocol.Logging;
using OmniSharp.Roslyn;
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
                _provider.SetProvider(server, (category, level) => LogFilter(category, level, environment));
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _provider?.CreateLogger(categoryName) ?? NullLogger.Instance;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private static bool LogFilter(string category, LogLevel level, IOmniSharpEnvironment environment)
        {
            if (environment.LogLevel > level)
            {
                return false;
            }

            if (!category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category, typeof(WorkspaceInformationService).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category, typeof(ProjectEventForwarder).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
