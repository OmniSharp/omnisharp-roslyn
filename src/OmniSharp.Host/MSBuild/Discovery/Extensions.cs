using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild.Discovery
{
    internal static class Extensions
    {
        public static void RegisterDefaultInstance(this IMSBuildLocator msbuildLocator, ILogger logger)
        {
            MSBuildInstance instanceToRegister = null;
            var invalidVSFound = false;

            foreach (var instance in msbuildLocator.GetInstances())
            {
                if (instance.IsInvalidVisualStudio())
                {
                    invalidVSFound = true;
                }
                else
                {
                    instanceToRegister = instance;
                    break;
                }
            }


            if (instanceToRegister != null)
            {
                // Did we end up choosing the standalone MSBuild because there was an invalid Visual Studio?
                // If so, provide a helpful message to the user.
                if (invalidVSFound && instanceToRegister.DiscoveryType == DiscoveryType.StandAlone)
                {
                    logger.LogWarning(@"It looks like you have Visual Studio 2017 RTM installed.
Try updating Visual Studio 2017 to the most recent release to enable better MSBuild support.");
                }

                msbuildLocator.RegisterInstance(instanceToRegister);
            }
            else
            {
                logger.LogError("Could not locate MSBuild instance to register with OmniSharp");
            }
        }


        public static bool IsInvalidVisualStudio(this MSBuildInstance instance)
            // MSBuild from Visual Studio 2017 RTM cannot be used.
            => instance.Version.Major == 15
            && instance.Version.Minor == 0
            && (instance.DiscoveryType == DiscoveryType.DeveloperConsole
                || instance.DiscoveryType == DiscoveryType.VisualStudioSetup);
    }
}
