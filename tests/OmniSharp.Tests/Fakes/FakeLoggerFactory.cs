using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Tests
{
    public class FakeLoggerFactory : ILoggerFactory
    {
        public LogLevel MinimumLevel { get; set; }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string name)
        {
            return NullLogger.Instance;
        }
    }
}
