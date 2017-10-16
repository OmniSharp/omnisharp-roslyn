using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Logging;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;
using OmniSharp.Utilities;

namespace OmniSharp.LanguageServerProtocol.Logging
{
    class LanguageServerLogger : ILogger
    {
        private readonly LanguageServerLoggerProvider _provider;
        protected readonly string CategoryName;
        private readonly bool _addHeader;
        private readonly CachedStringBuilder _cachedBuilder;

        private static readonly string s_padding = new string(' ', 8);
        private static readonly string s_newLinePlusPadding = Environment.NewLine + s_padding;

        public LanguageServerLogger(LanguageServerLoggerProvider provider, string categoryName, bool addHeader = true)
        {
            this._provider = provider;
            this.CategoryName = categoryName;
            this._addHeader = addHeader;
            this._cachedBuilder = new CachedStringBuilder();
        }

        private string CreateMessage(LogLevel logLevel, string messageText, Exception exception)
        {
            var builder = _cachedBuilder.Acquire();
            try
            {
                if (!string.IsNullOrEmpty(messageText))
                {
                    if (_addHeader)
                    {
                        builder.Append(GetLogLevelPrefix(logLevel));
                        builder.Append(this.CategoryName);
                        builder.AppendLine();

                        builder.Append(s_padding);
                        var length = builder.Length;
                        builder.Append(messageText);
                        builder.Replace(Environment.NewLine, s_newLinePlusPadding, length, messageText.Length);
                    }
                    else
                    {
                        builder.Append(messageText);
                    }
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
            _provider._filter != null
                ? _provider._filter(this.CategoryName, logLevel)
                : true;

        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }

        protected void WriteMessage(LogLevel logLevel, string message)
        {
            if (_provider._server == null) return;
            var messageType = GetMessageType(logLevel);
            if (messageType.HasValue)
            {
                _provider._server.LogMessage(new LogMessageParams()
                {
                    Type = messageType.Value,
                    Message = message
                });
            }
        }

        private MessageType? GetMessageType(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Critical:
                case LogLevel.Error:
                    return MessageType.Error;

                case LogLevel.Warning:
                    return MessageType.Warning;

                case LogLevel.Information:
                    return MessageType.Info;

                case LogLevel.Debug:
                case LogLevel.Trace:
                    return MessageType.Log;
            }
            return null;
        }
    }
}
