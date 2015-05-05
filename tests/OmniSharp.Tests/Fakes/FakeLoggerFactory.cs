using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Tests
{
    public class FakeLoggerFactory : ILoggerFactory
    {
        private class NullLogger : ILogger, IDisposable
        {
            private NullLogger()
            {
            }

            public readonly static ILogger Instance = new NullLogger();

            public IDisposable BeginScope(object state)
            {
                return this;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
            {
            }

            void IDisposable.Dispose()
            {
            }
        }

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