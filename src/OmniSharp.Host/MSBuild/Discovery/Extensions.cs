using System;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.MSBuild.Discovery
{
    internal static class Extensions
    {
        private static readonly Version s_minimumMSBuildVersion = new Version(16, 3);

        public static void RegisterDefaultInstance(this IMSBuildLocator msbuildLocator, ILogger logger, DotNetInfo dotNetInfo = null)
        {
            var minimumMSBuildVersion = GetSdkMinimumMSBuildVersion(dotNetInfo, logger);
            logger.LogDebug($".NET SDK requires MSBuild instances version {minimumMSBuildVersion} or higher");

            var bestInstanceFound = GetBestInstance(msbuildLocator, minimumMSBuildVersion, logger, out var invalidVSFound, out var vsWithoutSdkResolver);

            if (bestInstanceFound != null)
            {
                if (bestInstanceFound.Version < minimumMSBuildVersion)
                {
                    if (bestInstanceFound.DiscoveryType == DiscoveryType.Mono)
                    {
                        logger.LogWarning(
                            $@"It looks like you have Mono installed which contains a MSBuild lower than {minimumMSBuildVersion} which is the minimum supported by the configured .NET Core Sdk.
 Try updating Mono to the latest stable or preview version to enable better .NET Core Sdk support."
                        );
                    }
                    else if (bestInstanceFound.DiscoveryType == DiscoveryType.VisualStudioSetup)
                    {
                        logger.LogWarning(
                            $@"It looks like you have Visual Studio 2019 installed which contains a MSBuild lower than {minimumMSBuildVersion} which is the minimum supported by the configured .NET Core Sdk.
 Try updating Visual Studio to version {minimumMSBuildVersion} or higher to enable better .NET Core Sdk support."
                        );
                    }
                    else if (bestInstanceFound.DiscoveryType == DiscoveryType.UserOverride)
                    {
                        logger.LogWarning(
                            $@"It looks like you have overridden the version of MSBuild with a version lower than {minimumMSBuildVersion} which is the minimum supported by the configured .NET Core Sdk.
 Try updating your MSBuild to version {minimumMSBuildVersion} or higher to enable better .NET Core Sdk support."
                        );
                    }
                }

                msbuildLocator.RegisterInstance(bestInstanceFound);
            }
            else
            {
                throw new MSBuildNotFoundException("Could not locate MSBuild instance to register with OmniSharp.");
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
        /// Checks if the discovered Visual Studio instance includes a version of MSBuild lower than our minimum supported version.
        /// </summary>
        public static bool IsInvalidVisualStudio(this MSBuildInstance instance)
            => (instance.Version.Major < s_minimumMSBuildVersion.Major ||
                (instance.Version.Major == s_minimumMSBuildVersion.Major && instance.Version.Minor < s_minimumMSBuildVersion.Minor))
                && (instance.DiscoveryType == DiscoveryType.DeveloperConsole
                    || instance.DiscoveryType == DiscoveryType.VisualStudioSetup);

        public static MSBuildInstance GetBestInstance(this IMSBuildLocator msbuildLocator, Version minimumMSBuildVersion, ILogger logger, out bool invalidVSFound, out bool vsWithoutSdkResolver)
        {
            invalidVSFound = false;
            vsWithoutSdkResolver = false;
            MSBuildInstance bestMatchInstance = null;
            var bestMatchScore = 0;

            foreach (var instance in msbuildLocator.GetInstances())
            {
                var score = GetInstanceFeatureScore(instance, minimumMSBuildVersion);

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

        private static int GetInstanceFeatureScore(MSBuildInstance i, Version minimumMSBuildVersion)
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

            return score;
        }

        public static Version GetSdkMinimumMSBuildVersion(DotNetInfo dotNetInfo, ILogger logger)
        {
            if (dotNetInfo is null
                || string.IsNullOrWhiteSpace(dotNetInfo.SdksPath)
                || dotNetInfo.SdkVersion is null)
            {
                return s_minimumMSBuildVersion;
            }

            var version = dotNetInfo.SdkVersion;
            var sdksPath = dotNetInfo.SdksPath;
            var minimumVersionPath = Path.Combine(sdksPath, version.ToString(), "minimumMSBuildVersion");

            if (!File.Exists(minimumVersionPath))
            {
                logger.LogDebug($"Unable to locate minimumMSBuildVersion file at '{minimumVersionPath}'");
                return s_minimumMSBuildVersion;
            }

            var minimumVersionText = File.ReadAllText(minimumVersionPath);
            return Version.Parse(minimumVersionText);
        }
    }
}
