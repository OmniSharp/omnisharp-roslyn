using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.Discovery.Providers
{
    internal class MonoInstanceProvider : MSBuildInstanceProvider
    {
        public MonoInstanceProvider(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override ImmutableArray<MSBuildInstance> GetInstances()
        {
            if (!PlatformHelper.IsMono)
            {
                return NoInstances;
            }

            // Don't try to resolve to MSBuild assemblies under the installed Mono path unless OmniSharp
            // is actually running on the installed Mono runtime. The problem is that, when running standalone
            // on its own embedded Mono runtime, OmniSharp carries its own "GAC" of dependencies.
            // And, loading Microsoft.Build.* assemblies from the installed Mono location might have different
            // dependencies that aren't included in OmniSharp's GAC. This can result in strange failures during
            // design-time build that are difficult to diagnose. However, if OmniSharp is actually running on the
            // installed Mono runtime, Mono's GAC will be used and dependencies will be properly located. So, in
            // that case we can use the Microsoft.Build.* assemblies located under the installed Mono path.

            var monoRuntimePath = PlatformHelper.GetMonoRuntimePath();
            if (monoRuntimePath == null)
            {
                Logger.LogDebug("Could not retrieve Mono runtime path");
                return NoInstances;
            }

            string processFileName;
            try
            {
                processFileName = Process.GetCurrentProcess().MainModule.FileName;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to retrieve current process file name");
                return NoInstances;
            }

            if (!string.Equals(processFileName, monoRuntimePath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogDebug("Can't use installed Mono because OmniSharp isn't running on it");
                return NoInstances;
            }

            var path = PlatformHelper.GetMonoMSBuildDirPath();
            if (path == null)
            {
                return NoInstances;
            }

            // Double-check Mono version.
            // .NET Core 3.0 is not supported in MSBuild before 6.4.0
            var monoVersion = PlatformHelper.GetMonoVersion();
            if (monoVersion == null)
            {
                Logger.LogDebug("Could not retrieve Mono version");
                return NoInstances;
            }

            if (monoVersion < new Version("6.4.0"))
            {
                Logger.LogInformation($"Found Mono MSBuild but it could not be used because it is version {monoVersion} and it needs to be >= 6.4.0. OmniSharp will use embedded Mono instead. If you encounter any problems with OmniSharp, it is recommended to upgrade your Mono to the latest version. See https://www.mono-project.com/download/stable/.");
                return NoInstances;
            }

            var toolsPath = FindMSBuildToolsPath(path);
            if (toolsPath == null)
            {
                Logger.LogWarning($"Mono MSBuild could not be used because an MSBuild tools path could not be found.");
                return NoInstances;
            }

            // Look for Microsoft.Build.dll in the tools path. If it isn't there, this is likely a Mono layout on Linux
            // where the 'msbuild' package has not been installed.
            var microsoftBuildPath = Path.Combine(toolsPath, "Microsoft.Build.dll");
            if (!File.Exists(microsoftBuildPath))
            {
                Logger.LogDebug($"Mono MSBuild could not be used because '{microsoftBuildPath}' does not exist.");

                if (Platform.Current.OperatingSystem == Utilities.OperatingSystem.Linux)
                {
                    Logger.LogWarning(@"It looks like you have Mono 6.4.0 or greater installed but MSBuild could not be found.
Try installing MSBuild into Mono (e.g. 'sudo apt-get install msbuild') to enable better MSBuild support, or upgrade to latest Mono: https://www.mono-project.com/download/stable/.");
                }

                return NoInstances;
            }

            var propertyOverrides = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

            var localMSBuildPath = FindLocalMSBuildDirectory();
            if (localMSBuildPath != null)
            {
                var localRoslynPath = Path.Combine(localMSBuildPath, "Current", "Bin", "Roslyn");
                propertyOverrides.Add("CscToolPath", localRoslynPath);
                propertyOverrides.Add("CscToolExe", "csc.exe");
            }

            return ImmutableArray.Create(
                new MSBuildInstance(
                    nameof(DiscoveryType.Mono),
                    toolsPath,
                    new Version(16, 4),
                    DiscoveryType.Mono,
                    propertyOverrides.ToImmutable()));
        }
    }
}
