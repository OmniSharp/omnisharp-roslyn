using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild.Discovery
{
    internal abstract class MSBuildInstanceProvider
    {
        protected readonly ILogger Logger;

        protected MSBuildInstanceProvider(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger(this.GetType());
        }

        public abstract ImmutableArray<MSBuildInstance> GetInstances();
    }
}
