using System;

namespace Microsoft.Extensions.Logging
{
    public class DummyLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => new Disposable();

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }

        private class Disposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
