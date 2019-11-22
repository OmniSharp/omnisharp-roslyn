using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class StandAloneInstanceProvider : MSBuildInstanceProvider
    {
        public StandAloneInstanceProvider(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            var path = FindLocalMSBuildDirectory();
            if (path == null)
            {
                return NoInstances;
            }

            var extensionsPath = path;
            var toolsPath = Path.Combine(extensionsPath, "Current", "Bin");
            var roslynPath = Path.Combine(toolsPath, "Roslyn");

            var propertyOverrides = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

            if (PlatformHelper.IsMono)
            {
                // This disables a hack in the "GetReferenceAssemblyPaths" task which attempts
                // guarantee that .NET Framework SP1 is installed when the target framework is
                // .NET Framework, but the version is less than 4.0. The hack attempts to locate
                // a particular assembly in the GAC as a "guarantee". However, we don't include that
                // in our Mono package. So, we'll just bypass the check.
                propertyOverrides.Add("BypassFrameworkInstallChecks", "true");
            }

            propertyOverrides.Add("MSBuildToolsPath", toolsPath);
            propertyOverrides.Add("MSBuildExtensionsPath", extensionsPath);
            propertyOverrides.Add("CscToolPath", roslynPath);
            propertyOverrides.Add("CscToolExe", "csc.exe");

            return ImmutableArray.Create(
                new MSBuildInstance(
                    nameof(DiscoveryType.StandAlone),
                    toolsPath,
                    new Version(16, 3), // we now ship with embedded MsBuild 16.3
                    DiscoveryType.StandAlone,
                    propertyOverrides.ToImmutable(),
                    setMSBuildExePathVariable: true));
        }
    }
}
