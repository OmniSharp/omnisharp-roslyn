using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;
using MicrosoftBuildLocator = Microsoft.Build.Locator.MSBuildLocator;
using MicrosoftDiscoveryType = Microsoft.Build.Locator.DiscoveryType;

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
            // Restrict instances to NET 5 SDK
            var instances = MicrosoftBuildLocator.QueryVisualStudioInstances()
                .Where(instance => instance.Version.Major == 5);
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
