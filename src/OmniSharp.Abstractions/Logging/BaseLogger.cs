﻿using System;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Logging
{
    public abstract class BaseLogger : ILogger
    {
        protected readonly string CategoryName;
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly CachedStringBuilder _cachedBuilder;

        private static readonly string s_padding = new string(' ', 8);
        private static readonly string s_newLinePlusPadding = Environment.NewLine + s_padding;

        protected BaseLogger(string categoryName, Func<string, LogLevel, bool> filter = null)
        {
            this.CategoryName = categoryName;
            this._filter = filter;
            this._cachedBuilder = new CachedStringBuilder();
        }

        protected abstract void WriteMessage(LogLevel logLevel, string message);

        private string CreateMessage(LogLevel logLevel, string messageText, Exception exception)
        {
            var builder = _cachedBuilder.Acquire();
            try
            {
                if (!string.IsNullOrEmpty(messageText))
                {
                    builder.Append(GetLogLevelPrefix(logLevel));
                    builder.Append(this.CategoryName);
                    builder.AppendLine();

                    builder.Append(s_padding);
                    var length = builder.Length;
                    builder.Append(messageText);
                    builder.Replace(Environment.NewLine, s_newLinePlusPadding, length, messageText.Length);
                }

                if (exception != null)
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append(exception.ToString());
                }

                return builder.ToString();
            }
            finally
            {
                _cachedBuilder.Release(builder);
            }
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

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var messageText = formatter(state, exception);
            if (!string.IsNullOrEmpty(messageText) || exception != null)
            {
                var message = CreateMessage(logLevel, messageText, exception);
                WriteMessage(logLevel, message);
            }
        }

        public bool IsEnabled(LogLevel logLevel) =>
            _filter != null
                ? _filter(this.CategoryName, logLevel)
                : true;

        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
