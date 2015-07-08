using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Tests
{
    public class FakeLogger : ILogger
    {
        public void Log(LogLevel level, int number, object obj, Exception ex, Func<object, Exception, string> iThinkThisIsTheNextLoggerMaybe)
        {
        }

        public bool IsEnabled(LogLevel level)
        {
            return true;
        }

        public IDisposable BeginScope(object owner)
        {
            return null;
        }

        public IDisposable BeginScopeImpl(object state)
        {
            return null;
        }
    }
}
