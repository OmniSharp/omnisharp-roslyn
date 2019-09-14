using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class StandAloneInstanceProvider : MSBuildInstanceProvider
    {
        private readonly bool _allowMonoPaths;

        public StandAloneInstanceProvider(ILoggerFactory loggerFactory, bool allowMonoPaths)
            : base(loggerFactory)
        {
            _allowMonoPaths = allowMonoPaths;
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

                // To better support older versions of Mono that don't include
                // MSBuild 15, we attempt to set property overrides to the locations
                // of Mono's 'xbuild' and 'xbuild-frameworks' paths.
                if (_allowMonoPaths)
                {
                    if (TryGetMonoXBuildPath(out var xbuildPath))
                    {
                        extensionsPath = xbuildPath;
                    }

                    if (TryGetMonoXBuildFrameworksPath(out var xbuildFrameworksPath))
                    {
                        propertyOverrides.Add("TargetFrameworkRootPath", xbuildFrameworksPath);
                    }
                }
            }

            propertyOverrides.Add("MSBuildToolsPath", toolsPath);
            propertyOverrides.Add("MSBuildExtensionsPath", extensionsPath);
            propertyOverrides.Add("CscToolPath", roslynPath);
            propertyOverrides.Add("CscToolExe", "csc.exe");

            return ImmutableArray.Create(
                new MSBuildInstance(
                    nameof(DiscoveryType.StandAlone),
                    toolsPath,
                    new Version(16, 0), // we now ship with embedded MsBuild 16.x
                    DiscoveryType.StandAlone,
                    propertyOverrides.ToImmutable(),
                    setMSBuildExePathVariable: true));
        }

        private static bool TryGetMonoXBuildPath(out string path)
        {
            path = null;

            var monoXBuildDirPath = PlatformHelper.GetMonoXBuildDirPath();
            if (monoXBuildDirPath == null)
            {
                return false;
            }

            var monoXBuild15DirPath = Path.Combine(monoXBuildDirPath, "15.0");
            if (Directory.Exists(monoXBuild15DirPath))
            {
                path = monoXBuildDirPath;
                return true;
            }


            var monoXBuildCurrentDirPath = Path.Combine(monoXBuildDirPath, "Current");
            if (Directory.Exists(monoXBuildCurrentDirPath))
            {
                path = monoXBuildDirPath;
                return true;
            }

            return false;
        }

        private static bool TryGetMonoXBuildFrameworksPath(out string path)
        {
            path = null;

            var monoMSBuildXBuildFrameworksDirPath = PlatformHelper.GetMonoXBuildFrameworksDirPath();
            if (monoMSBuildXBuildFrameworksDirPath == null)
            {
                return false;
            }

            path = monoMSBuildXBuildFrameworksDirPath;
            return true;
        }
    }
}
