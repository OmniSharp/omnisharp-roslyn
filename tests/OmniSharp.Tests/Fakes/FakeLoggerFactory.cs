using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Tests
{
    public class FakeLoggerFactory : ILoggerFactory
    {
        private static FakeLogger logger = new FakeLogger();
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public LogLevel MinimumLevel { get; set; } = LogLevel.Verbose;

        public ILogger CreateLogger(string name)
        {
            return logger;
        }

        public void Dispose() { }
    }
}
