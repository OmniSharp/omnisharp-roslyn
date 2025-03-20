using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Eventing
{
    public interface IEventEmitter
    {
        ValueTask EmitAsync(string kind, object args, CancellationToken cancellationToken = default);
    }
}
