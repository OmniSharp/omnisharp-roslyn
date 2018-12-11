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
                    instance.MSBuildPath
                    , "SdkResolvers"
                    , dotnetSdkResolver
                    , dotnetSdkResolver + ".dll"
                )
            );
        }

        public static bool IsInvalidVisualStudio(this MSBuildInstance instance)

            // MSBuild from Visual Studio 2017 RTM cannot be used.
            => instance.Version.Major == 15
                && instance.Version.Minor == 0
                && (instance.DiscoveryType == DiscoveryType.DeveloperConsole
                    || instance.DiscoveryType == DiscoveryType.VisualStudioSetup);

        public static MSBuildInstance GetBestInstance(this IMSBuildLocator msbuildLocator, out bool invalidVSFound)
        {
            invalidVSFound = false;
            MSBuildInstance bestMatchInstance = null;
            var bestMatchScore = -1;

            // same as (without so many allocations)
            // var bestMatchInstance =  msbuildLocator
            //     .GetInstances()
            //     .Select(i => (instance: i, featureScore: GetFeatureScore(i)))
            //     .OrderByDescending(i => i.featureScore)
            //     .ThenByDescending(i => i.instance.Version.Major)
            //     .Select(i => i.instance)
            //     .FirstOrDefault();

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

            if (i.HasDotNetSdksResolvers())
                score++;

            if (i.IsInvalidVisualStudio())
                score--;
            else
                score++;

            if (i.DiscoveryType == DiscoveryType.StandAlone)
                score--;

            return score;
        }
    }
}
