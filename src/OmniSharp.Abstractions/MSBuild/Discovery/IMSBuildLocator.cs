using System.Collections.Immutable;

namespace OmniSharp.MSBuild.Discovery
{
    public interface IMSBuildLocator
    {
        MSBuildInstance RegisteredInstance { get; }

        void RegisterInstance(MSBuildInstance instance);
        ImmutableArray<MSBuildInstance> GetInstances();
    }
}
