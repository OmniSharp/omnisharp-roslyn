using System.Collections.Generic;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class MSBuildOverrideOptions
    {
        public Dictionary<string, string> PropertyOverrides { get; set; }

        public string MSBuildPath { get; set; }

        public string Name { get; set; }
    }
}
