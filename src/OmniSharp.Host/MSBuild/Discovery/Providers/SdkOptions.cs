using System.Collections.Generic;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class SdkOptions
    {
        public Dictionary<string, string> PropertyOverrides { get; set; }

        public string Path { get; set; }

        public string Version { get; set; }

        public bool IncludePrereleases { get; set; }
    }
}
