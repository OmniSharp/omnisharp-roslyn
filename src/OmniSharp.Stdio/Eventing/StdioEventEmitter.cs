using OmniSharp.Eventing;
using OmniSharp.Protocol;
using OmniSharp.Services;

namespace OmniSharp.Stdio.Eventing
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
            var packet = new EventPacket
            {
                Event = kind,
                Body = args
            };

            _writer.WriteLine(packet);
        }
    }
}
