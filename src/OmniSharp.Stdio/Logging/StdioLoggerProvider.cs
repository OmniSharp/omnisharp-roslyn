using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio.Logging
{
    class StdioLoggerProvider : ILoggerProvider
    {
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly ISharedTextWriter _writer;

        public StdioLoggerProvider(ISharedTextWriter writer, Func<string, LogLevel, bool> filter)
        {
            _writer = writer;
            _filter = filter;
        }

        public ILogger CreateLogger(string name)
        {
            return new StdioLogger(_writer, name, _filter);
        }

        public void Dispose() { }
    }
}
