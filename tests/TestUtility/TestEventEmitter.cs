using System;
using System.Collections.Immutable;
using System.Composition.Hosting.Core;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Mef;
using OmniSharp.MSBuild;
using OmniSharp.MSBuild.Notification;

namespace TestUtility
{
    public class TestEventEmitter : IEventEmitter
    {
        public ImmutableArray<object> Messages { get; private set; } = ImmutableArray<object>.Empty;

        public async Task WaitForEvent<T>(Predicate<T> predicate = null, int frequency = 25, int timeoutMs = 15000)
        {
            var waitTask = Task.Run(async () =>
            {
                while (!Messages.OfType<T>().Any() && predicate(Messages.OfType<T>().First())) await Task.Delay(frequency);
            });

            if (waitTask != await Task.WhenAny(waitTask,
                    Task.Delay(timeoutMs)))
                throw new TimeoutException($"Timeout of {timeoutMs} ms exceeded before any matching message for type {typeof(T)} received or precondition for that type failed (if any).");
        }

        public void Clear()
        {
            Messages = ImmutableArray<object>.Empty;
        }

        public void Emit(string kind, object args)
        {
            Messages = Messages.Add(args);
        }

        public ExportDescriptorProvider[] AsExportDescriptionProvider(ILoggerFactory loggerFactory)
        {
            var listener = new ProjectLoadListener(loggerFactory, this);

            return new ExportDescriptorProvider[]
            {
                    MefValueProvider.From<IMSBuildEventSink>(listener)
            };
        }
    }
}