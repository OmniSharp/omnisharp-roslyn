using Microsoft.Extensions.Logging;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Services
{
    public class StdioEventEmitter : IEventEmitter
    {
        private readonly ILogger _logger;
        private readonly ISharedTextWriter _writer;

        public StdioEventEmitter(ISharedTextWriter writer)
        {
            _writer = writer;
        }

        public StdioEventEmitter(ISharedTextWriter writer, ILoggerFactory loggerFactory)
        {
            _writer = writer;
            _logger = loggerFactory?.CreateLogger<StdioEventEmitter>();
        }

        public void Emit(string kind, object args)
        {
            var packet = new EventPacket()
            {
                Event = kind,
                Body = args
            };

            //_logger?.LogInformation(packet.ToString());

            _writer.WriteLineAsync(new EventPacket()
            {
                Event = kind,
                Body = args
            });
        }
    }
}