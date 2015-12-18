using System;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Tests
{
    public class FakeLogger : ILogger
    {
        public bool IsEnabled(LogLevel level) => true;

        public IDisposable BeginScopeImpl(object owner) => new NoopDisposable();

        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter) { }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
