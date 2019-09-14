using System.IO;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild.Discovery
{
    internal static class Extensions
    {
        public static void RegisterDefaultInstance(this IMSBuildLocator msbuildLocator, ILogger logger)
        {
            var bestInstanceFound = GetBestInstance(msbuildLocator, out var invalidVSFound);

            if (bestInstanceFound != null)
            {
                // Did we end up choosing the standalone MSBuild because there was an invalid Visual Studio?
                // If so, provide a helpful message to the user.
                if (invalidVSFound && bestInstanceFound.DiscoveryType == DiscoveryType.StandAlone)
                {
                    logger.LogWarning(
                        @"It looks like you have Visual Studio 2017 RTM installed.
 Try updating Visual Studio 2017 to the most recent release to enable better MSBuild support."
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
            => instance.Version.Major == 15
                && instance.Version.Minor == 0
                && (instance.DiscoveryType == DiscoveryType.DeveloperConsole
                    || instance.DiscoveryType == DiscoveryType.VisualStudioSetup);

        public static MSBuildInstance GetBestInstance(this IMSBuildLocator msbuildLocator, out bool invalidVSFound)
        {
            invalidVSFound = false;
            MSBuildInstance bestMatchInstance = null;
            var bestMatchScore = 0;

            foreach (var instance in msbuildLocator.GetInstances())
            {
                var score = GetInstanceFeatureScore(instance);

                invalidVSFound = invalidVSFound || instance.IsInvalidVisualStudio();

                if (score > bestMatchScore
                    || (score == bestMatchScore && instance.Version.Major > (bestMatchInstance?.Version.Major ?? 0)))
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

            if (i.HasDotNetSdksResolvers())
                score++;

            if (i.IsInvalidVisualStudio())
                return int.MinValue;
            else
                score++;

            if (i.DiscoveryType == DiscoveryType.StandAlone)
                score--;

            // VS 2019 should be preferred
            if (i.Version.Major >= 16)
                score++;
            else
                score--;

            return score;
        }
    }
}
