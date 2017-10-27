using System;
using System.Collections.Immutable;

namespace OmniSharp.MSBuild.Discovery
{
    public class MSBuildInstance
    {
        public string Name { get; }
        public string MSBuildPath { get; }
        public Version Version { get; }
        public DiscoveryType DiscoveryType { get; }
        public ImmutableDictionary<string, string> PropertyOverrides { get; }
        public bool SetMSBuildExePathVariable { get; }

        public MSBuildInstance(
            string name, string msbuildPath, Version version, DiscoveryType discoveryType,
            ImmutableDictionary<string, string> propertyOverrides = null,
            bool setMSBuildExePathVariable = false)
        {
            Name = name;
            MSBuildPath = msbuildPath;
            Version = version ?? new Version(0, 0);
            DiscoveryType = discoveryType;
            PropertyOverrides = propertyOverrides ?? ImmutableDictionary<string, string>.Empty;
            SetMSBuildExePathVariable = setMSBuildExePathVariable;
        }

        public override string ToString()
            => $"{Name} {Version} - \"{MSBuildPath}\"";
    }
}
