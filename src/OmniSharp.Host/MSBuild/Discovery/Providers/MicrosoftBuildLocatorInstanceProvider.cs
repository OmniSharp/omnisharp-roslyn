using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using MicrosoftBuildLocator = Microsoft.Build.Locator.MSBuildLocator;
using MicrosoftDiscoveryType = Microsoft.Build.Locator.DiscoveryType;
#if !NETCOREAPP
using OmniSharp.Utilities;
#endif

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class MicrosoftBuildLocatorInstanceProvider : MSBuildInstanceProvider
    {
        public MicrosoftBuildLocatorInstanceProvider(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {

#if NETCOREAPP
            // Restrict instances to NET 6 SDK
            var instances = MicrosoftBuildLocator.QueryVisualStudioInstances()
                .Where(instance => instance.Version.Major == 6)
                .OrderByDescending(instance => instance.Version)
                .ToImmutableArray();

            if (instances.Length == 0)
            {
                Logger.LogError($"OmniSharp requires the .NET 6 SDK be installed. Please visit https://dotnet.microsoft.com/download/dotnet/6.0 to download the .NET SDK.");
            }
#else
            if (!PlatformHelper.IsWindows)
            {
                return NoInstances;
            }

            var instances = MicrosoftBuildLocator.QueryVisualStudioInstances();
#endif

            return instances.Select(instance =>
            {
                var microsoftBuildPath = Path.Combine(instance.MSBuildPath, "Microsoft.Build.dll");
                var version = GetMSBuildVersion(microsoftBuildPath);

                return new MSBuildInstance(
                    $"{instance.Name} {instance.Version}",
                    instance.MSBuildPath,
                    version,
                    GetDiscoveryType(instance.DiscoveryType));
            }).ToImmutableArray();

            static DiscoveryType GetDiscoveryType(MicrosoftDiscoveryType discoveryType)
            {
                return discoveryType switch
                {
                    MicrosoftDiscoveryType.DeveloperConsole => DiscoveryType.DeveloperConsole,
                    MicrosoftDiscoveryType.VisualStudioSetup => DiscoveryType.VisualStudioSetup,
                    MicrosoftDiscoveryType.DotNetSdk => DiscoveryType.DotNetSdk,
                    _ => throw new ArgumentException()
                };
            }
        }
    }
}
