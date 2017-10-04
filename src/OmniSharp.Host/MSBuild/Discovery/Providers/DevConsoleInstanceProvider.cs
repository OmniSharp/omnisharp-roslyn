using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class DevConsoleInstanceProvider : MSBuildInstanceProvider
    {
        public DevConsoleInstanceProvider(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            if (!PlatformHelper.IsWindows)
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var path = Environment.GetEnvironmentVariable("VSINSTALLDIR");

            if (string.IsNullOrEmpty(path))
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var versionString = Environment.GetEnvironmentVariable("VSCMD_VER");
            Version.TryParse(versionString, out var version);

            if (version == null)
            {
                versionString = Environment.GetEnvironmentVariable("VisualStudioVersion");
                Version.TryParse(versionString, out version);
            }

            return ImmutableArray.Create(
                new MSBuildInstance(
                    "DEVCONSOLE",
                    Path.Combine(path, "MSBuild", "15.0", "Bin"),
                    version,
                    DiscoveryType.DeveloperConsole));
        }
    }
}
