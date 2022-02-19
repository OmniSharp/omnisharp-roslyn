using System;

namespace OmniSharp.MSBuild.Discovery
{
    public enum DiscoveryType
    {
        [Obsolete("The StandAlone type is no longer supported.")]
        StandAlone = 0,
        DeveloperConsole = 1,
        VisualStudioSetup = 2,
        Mono = 3,
        UserOverride = 4,
        DotNetSdk = 5
    }
}
