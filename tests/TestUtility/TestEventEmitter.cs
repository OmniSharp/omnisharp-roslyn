using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Eventing;

namespace TestUtility
{
    public class TestEventEmitter : IEventEmitter
    {
        public ImmutableArray<object> Messages { get; private set; } = ImmutableArray<object>.Empty;

        public async Task WaitForEvent<T>(Predicate<T> predicate = null, int frequency = 25, int timeoutMs = 15000)
        {
            if (predicate == null)
                predicate = _ => true;

            var waitTask = Task.Run(async () =>
            {
                while (!Messages.OfType<T>().Any() && !Messages.OfType<T>().Any(x => predicate(x))) await Task.Delay(frequency);
            });

            if (waitTask != await Task.WhenAny(waitTask,
                    Task.Delay(timeoutMs)))
                throw new TimeoutException($"Timeout of {timeoutMs} ms exceeded before any matching message for type {typeof(T)} received or precondition for that type failed (if any), received messages this far: {string.Join(", ", Messages)}");
        }

        public void Clear()
        {
            Messages = ImmutableArray<object>.Empty;
        }

        public void Emit(string kind, object args)
        {
            Messages = Messages.Add(args);
        }
    }
}
