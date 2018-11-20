using System;
using System.Collections.Immutable;
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
                return NoInstances;
            }

            var path = Environment.GetEnvironmentVariable("VSINSTALLDIR");

            if (string.IsNullOrEmpty(path))
            {
                return NoInstances;
            }

            var toolsPath = FindMSBuildToolsPath(path);
            if (toolsPath == null)
            {
                return NoInstances;
            }

            var versionString = Environment.GetEnvironmentVariable("VSCMD_VER");
            Version.TryParse(versionString, out var version);

            if (version == null && versionString != null)
            {
                var dashIndex = versionString.IndexOf('-');

                if (dashIndex > 0)
                {
                    versionString = versionString.Substring(0, dashIndex);
                    Version.TryParse(versionString, out version);
                }
            }

            if (version == null)
            {
                versionString = Environment.GetEnvironmentVariable("VisualStudioVersion");
                Version.TryParse(versionString, out version);
            }

            return ImmutableArray.Create(
                new MSBuildInstance(
                    nameof(DiscoveryType.DeveloperConsole),
                    toolsPath,
                    version,
                    DiscoveryType.DeveloperConsole));
        }
    }
}
