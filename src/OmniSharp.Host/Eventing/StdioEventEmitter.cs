using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Eventing
{
    internal class StdioEventEmitter : IEventEmitter
    {
        private readonly ISharedTextWriter _writer;

        public StdioEventEmitter(ISharedTextWriter writer)
        {
            _writer = writer;
        }

        public void Emit(string kind, object args)
        {
            var packet = new EventPacket
            {
                Event = kind,
                Body = args
            };

            _writer.WriteLineAsync(packet);
        }
    }
}