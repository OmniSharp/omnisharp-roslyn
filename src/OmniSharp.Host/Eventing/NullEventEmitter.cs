using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Eventing
{
    public class NullEventEmitter : IEventEmitter
    {
        public static IEventEmitter Instance { get; } = new NullEventEmitter();

        private NullEventEmitter() { }

        public ValueTask EmitAsync(string kind, object args, CancellationToken cancellationToken = default) =>
            // nothing
            new();
    }
}
