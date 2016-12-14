using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public class TestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            this._output = output;
        }

        public bool IsEnabled(LogLevel level) => true;

        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var text = formatter(state, exception);
            if (!string.IsNullOrEmpty(text) || exception != null)
            {
                _output.WriteLine(text);
            }
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
