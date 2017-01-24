using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TestUtility.Logging
{
    public class TestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        private static readonly string s_padding = new string(' ', 8);
        private static readonly string s_newLinePlusPadding = Environment.NewLine + s_padding;

        [ThreadStatic]
        private static StringBuilder g_builder;

        public TestLogger(ITestOutputHelper output, string categoryName)
        {
            this._output = output;
            this._categoryName = categoryName;
        }

        private StringBuilder GetBuilder()
        {
            var builder = g_builder;
            g_builder = null;

            if (builder == null)
            {
                builder = new StringBuilder();
            }

            return builder;
        }

        private void ReleaseBuilder(StringBuilder builder)
        {
            builder.Clear();
            if (builder.Capacity > 1024)
            {
                builder.Capacity = 1024;
            }

            g_builder = builder;
        }

        private void WriteMessage(LogLevel logLevel, string message, Exception exception)
        {
            var builder = GetBuilder();

            if (!string.IsNullOrEmpty(message))
            {
                builder.Append(GetLogLevelPrefix(logLevel));
                builder.Append(_categoryName);
                builder.AppendLine();

                builder.Append(s_padding);
                var length = builder.Length;
                builder.AppendLine(message);
                builder.Replace(Environment.NewLine, s_newLinePlusPadding, length, message.Length);
            }

            if (exception != null)
            {
                builder.AppendLine(exception.ToString());
            }

            if (builder.Length > 0)
            {
                _output.WriteLine(builder.ToString());
            }

            ReleaseBuilder(builder);
        }

        private static string GetLogLevelPrefix(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return "[trce]: ";
                case LogLevel.Debug:
                    return "[dbug]: ";
                case LogLevel.Information:
                    return "[info]: ";
                case LogLevel.Warning:
                    return "[warn]: ";
                case LogLevel.Error:
                    return "[fail]: ";
                case LogLevel.Critical:
                    return "[crit]: ";
                default:
                    throw new ArgumentOutOfRangeException(nameof(LogLevel));
            }
        }

        public bool IsEnabled(LogLevel level) => true;

        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            if (!string.IsNullOrEmpty(message) || exception != null)
            {
                WriteMessage(logLevel, message, exception);
            }
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
