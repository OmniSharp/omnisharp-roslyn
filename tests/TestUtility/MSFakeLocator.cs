using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using OmniSharp.MSBuild.Discovery;

namespace TestUtility
{
    public class MSFakeLocator : IMSBuildLocator
    {
        private readonly ImmutableArray<MSBuildInstance> _instances;

        public MSBuildInstance RegisteredInstance { get; private set; }

        public MSFakeLocator(IEnumerable<MSBuildInstance> instances)
        {
            _instances = instances.ToImmutableArray();
        }

        public void RegisterInstance(MSBuildInstance instance)
            => RegisteredInstance = instance;

        public ImmutableArray<MSBuildInstance> GetInstances()
            => _instances;

        public void DeleteFakeInstancesFolders()
        {
            foreach (var instance in _instances)
            {
                if (Directory.Exists(instance.MSBuildPath))
                    Directory.Delete(instance.MSBuildPath, true);
            }
        }
    }
}
