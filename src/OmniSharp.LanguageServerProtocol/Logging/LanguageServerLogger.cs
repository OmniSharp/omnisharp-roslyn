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
    class LanguageServerLogger : BaseLogger
    {
        private readonly LanguageServerLoggerProvider _provider;

        public LanguageServerLogger(LanguageServerLoggerProvider provider, string categoryName, bool addHeader = true) : base(categoryName, provider._filter, addHeader)
        {
            _provider = provider;
        }

        protected override void WriteMessage(LogLevel logLevel, string message)
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
