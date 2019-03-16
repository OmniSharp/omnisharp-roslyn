using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.Stdio.Logging
{
    class StdioLoggerProvider : ILoggerProvider
    {
        private readonly ISharedTextWriter _writer;

        public StdioLoggerProvider(ISharedTextWriter writer)
        {
            _writer = writer;
        }

        public ILogger CreateLogger(string name)
        {
            return new StdioLogger(_writer, name);
        }

        public void Dispose() { }
    }
}
