using System;
using Microsoft.Extensions.Logging;

namespace TestUtility.Fake
{
    public class FakeLogger : ILogger
    {
        public bool IsEnabled(LogLevel level) => true;

        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
