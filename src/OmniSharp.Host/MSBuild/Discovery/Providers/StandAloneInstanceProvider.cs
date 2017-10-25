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
            var path = FindMSBuildDirectory();
            if (path == null)
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var extensionsPath = path;
            var toolsPath = Path.Combine(extensionsPath, "15.0", "Bin");
            var roslynPath = Path.Combine(toolsPath, "Roslyn");

            var propertyOverrides = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

            // To better support older versions of Mono that don't include
            // MSBuild 15, we attempt to set property overrides to the locations
            // of Mono's 'xbuild' and 'xbuild-frameworks' paths.
            if (_allowMonoPaths && PlatformHelper.IsMono)
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

            propertyOverrides.Add("MSBuildToolsPath", toolsPath);
            propertyOverrides.Add("MSBuildExtensionsPath", extensionsPath);
            propertyOverrides.Add("CscToolPath", roslynPath);
            propertyOverrides.Add("CscToolExe", "csc.exe");

            return ImmutableArray.Create(
                new MSBuildInstance(
                    "STANDALONE",
                    toolsPath,
                    new Version(15, 0),
                    DiscoveryType.StandAlone,
                    propertyOverrides.ToImmutable(),
                    setMSBuildExePathVariable: true));
        }

        private static string FindMSBuildDirectory()
        {
            // If OmniSharp is running normally, MSBuild is located in an 'msbuild' folder beneath OmniSharp.exe.
            var msbuildDirectory = Path.Combine(AppContext.BaseDirectory, "msbuild");

            if (!Directory.Exists(msbuildDirectory))
            {
                // If the 'msbuild' folder does not exists beneath "OmniSharp.exe", this is likely a development scenario,
                // such as debugging or running unit tests. In that case, we try to locate the ".msbuild" folder at the
                // solution level.
                msbuildDirectory = FindLocalMSBuildFromSolution();
            }

            return msbuildDirectory;
        }

        private static string FindLocalMSBuildFromSolution()
        {
            // Try to locate the .msbuild folder by search for the OmniSharp solution relative to the current folder.
            var current = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(current, "OmniSharp.sln")))
            {
                current = Path.GetDirectoryName(current);
                if (Path.GetPathRoot(current) == current)
                {
                    break;
                }
            }

            var result = Path.Combine(current, ".msbuild");

            return Directory.Exists(result)
                ? result
                : null;
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
            if (!Directory.Exists(monoXBuild15DirPath))
            {
                return false;
            }

            path = monoXBuildDirPath;
            return true;
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
