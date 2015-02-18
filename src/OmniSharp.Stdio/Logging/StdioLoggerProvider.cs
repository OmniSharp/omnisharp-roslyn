using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Stdio.Logging
{
    internal class StdioLoggerProvider : ILoggerProvider
    {
        private readonly Func<string, LogLevel, bool> _filter;

        internal StdioLoggerProvider(Func<string, LogLevel, bool> filter)
        {
            _filter = filter;
        }

        public ILogger Create(string name)
        {
            return new StdioLogger(name, _filter);
        }
    }
}
