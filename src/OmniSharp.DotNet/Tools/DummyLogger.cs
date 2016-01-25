using System;

namespace Microsoft.Extensions.Logging
{
    public class DummyLogger<T> : ILogger<T>
    {
        public IDisposable BeginScopeImpl(object state) => new Disposable();

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter) { }

        private class Disposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
