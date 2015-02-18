using System;
using Microsoft.Framework.Logging;
using OmniSharp.Stdio.Protocol;

namespace OmniSharp.Stdio.Logging
{
    internal class StdioLogger : ILogger
    {
        private readonly object _lock = new object();
        private readonly string _name;
        private readonly Func<string, LogLevel, bool> _filter;

        internal StdioLogger(string name, Func<string, LogLevel, bool> filter)
        {
            _name = name;
            _filter = filter;
        }

        public IDisposable BeginScope(object state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _filter(this._name, logLevel);
        }

        public void Write(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            string text = string.Empty;

            if (formatter != null)
            {
                text = formatter(state, exception);
            }
            else
            {
                if (state != null)
                {
                    text += state;
                }
                if (exception != null)
                {
                    text = text + Environment.NewLine + exception;
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var packet = new EventPacket()
            {
                Event = "log",
                Body = new
                {
                    LogLevel = logLevel.ToString().ToUpperInvariant(),
                    Name = this._name,
                    Message = text
                }
            };

            Console.WriteLine(packet);
        }
    }
}
