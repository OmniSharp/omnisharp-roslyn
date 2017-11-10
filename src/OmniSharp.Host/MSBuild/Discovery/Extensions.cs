namespace OmniSharp.MSBuild.Discovery
{
    internal static class Extensions
    {
        public static bool IsInvalidVisualStudio(this MSBuildInstance instance)
            // MSBuild from Visual Studio 2017 RTM cannot be used.
            => instance.Version.Major == 15
            && instance.Version.Minor == 0
            && (instance.DiscoveryType == DiscoveryType.DeveloperConsole
                || instance.DiscoveryType == DiscoveryType.VisualStudioSetup);

    }
}
