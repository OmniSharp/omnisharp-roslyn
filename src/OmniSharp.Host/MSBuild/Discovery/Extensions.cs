using System.IO;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild.Discovery
{
    internal static class Extensions
    {
        public static void RegisterDefaultInstance(this IMSBuildLocator msbuildLocator, ILogger logger)
        {
            var bestInstanceFound = GetBestInstance(msbuildLocator, logger, out var invalidVSFound, out var vsWithoutSdkResolver);

            if (bestInstanceFound != null)
            {
                // Did we end up choosing the standalone MSBuild because there was an invalid Visual Studio?
                // If so, provide a helpful message to the user.
                if (invalidVSFound && bestInstanceFound.DiscoveryType == DiscoveryType.StandAlone)
                {
                    logger.LogWarning(
                        @"It looks like you have Visual Studio lower than VS 2019 16.3 installed.
 Try updating Visual Studio to the most recent release to enable better MSBuild support."
                    );
                }

                if (vsWithoutSdkResolver && bestInstanceFound.DiscoveryType == DiscoveryType.StandAlone)
                {
                    logger.LogWarning(
                        @"It looks like you have Visual Studio 2019 installed without .NET Core SDK support which is required by OmniSharp.
 Try updating Visual Studio 2019 installation with .NET Core SDK to enable better MSBuild support."
                    );
                }

                msbuildLocator.RegisterInstance(bestInstanceFound);
            }
            else
            {
                logger.LogError("Could not locate MSBuild instance to register with OmniSharp");
            }
        }

        public static bool HasDotNetSdksResolvers(this MSBuildInstance instance)
        {
            const string dotnetSdkResolver = "Microsoft.DotNet.MSBuildSdkResolver";

            return File.Exists(
                Path.Combine(
                    instance.MSBuildPath,
                    "SdkResolvers",
                    dotnetSdkResolver,
                    dotnetSdkResolver + ".dll"
                )
            );
        }

        /// <summary>
        /// Checks if it is MSBuild from Visual Studio 2017 RTM that cannot be used.
        /// </summary>
        public static bool IsInvalidVisualStudio(this MSBuildInstance instance)
            => (instance.Version.Major < 16 ||
                (instance.Version.Major == 16 && instance.Version.Minor < 3))
                && (instance.DiscoveryType == DiscoveryType.DeveloperConsole
                    || instance.DiscoveryType == DiscoveryType.VisualStudioSetup);

        public static MSBuildInstance GetBestInstance(this IMSBuildLocator msbuildLocator, ILogger logger, out bool invalidVSFound, out bool vsWithoutSdkResolver)
        {
            invalidVSFound = false;
            vsWithoutSdkResolver = false;
            MSBuildInstance bestMatchInstance = null;
            var bestMatchScore = 0;

            foreach (var instance in msbuildLocator.GetInstances())
            {
                var score = GetInstanceFeatureScore(instance);

                logger.LogDebug($"MSBuild instance {instance.Name} {instance.Version} scored at {score}");

                invalidVSFound = invalidVSFound || instance.IsInvalidVisualStudio();
                vsWithoutSdkResolver = vsWithoutSdkResolver || (!instance.IsInvalidVisualStudio() && !instance.HasDotNetSdksResolvers());

                if (bestMatchInstance == null ||
                    score > bestMatchScore ||
                    score == bestMatchScore && instance.Version > bestMatchInstance.Version)
                {
                    bestMatchInstance = instance;
                    bestMatchScore = score;
                }
            }

            return bestMatchInstance;
        }

        private static int GetInstanceFeatureScore(MSBuildInstance i)
        {
            var score = 0;

            // user override gets highest priority
            if (i.DiscoveryType == DiscoveryType.UserOverride)
                return int.MaxValue;

            if (i.IsInvalidVisualStudio())
                return int.MinValue;
            else
                score++;

            // dotnet SDK resolvers are mandatory to use a VS instance
            if (i.HasDotNetSdksResolvers())
                score++;
            else
                return int.MinValue;

            if (i.DiscoveryType == DiscoveryType.StandAlone)
                score--;

            return score;
        }
    }
}
