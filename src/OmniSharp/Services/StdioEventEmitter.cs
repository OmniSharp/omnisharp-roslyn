using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Services
{
    public class StdioEventEmitter : IEventEmitter
    {
        private readonly ISharedTextWriter _writer;

        public StdioEventEmitter(ISharedTextWriter writer)
        {
            _writer = writer;
        }

        public void Emit(string kind, object args)
        {
            _writer.WriteLineAsync(new EventPacket()
            {
                Event = kind,
                Body = args
            });
        }
    }
}