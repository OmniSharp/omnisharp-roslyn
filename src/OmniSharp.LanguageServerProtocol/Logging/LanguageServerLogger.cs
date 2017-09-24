using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Logging;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.LanguageServerProtocol.Logging
{
    class LanguageServerLogger : BaseLogger
    {
        private readonly LanguageServer _server;

        public LanguageServerLogger(LanguageServer server, string categoryName, Func<string, LogLevel, bool> filter)
            : base(categoryName, filter)
        {
            _server = server;
        }

        protected override void WriteMessage(LogLevel logLevel, string message)
        {
            var messageType = GetMessageType(logLevel);
            if (messageType.HasValue)
            {
                _server.LogMessage(new LogMessageParams()
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
