using Microsoft.Extensions.Logging;
using OmniSharp.Logging;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio.Logging
{
    class StdioLogger : BaseLogger
    {
        private readonly ISharedTextWriter _writer;

        public StdioLogger(ISharedTextWriter writer, string categoryName)
            : base(categoryName, addHeader: false)
        {
            _writer = writer;
        }

        protected override void WriteMessage(LogLevel logLevel, string message)
        {
            var packet = new EventPacket()
            {
                Event = "log",
                Body = new
                {
                    LogLevel = logLevel.ToString().ToUpperInvariant(),
                    Name = this.CategoryName,
                    Message = message
                }
            };

            // don't block the logger
            _writer.WriteLineAsync(packet);
        }
    }
}
