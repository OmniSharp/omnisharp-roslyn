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

            // Don't resolve to MSBuild assemblies under the installed Mono unless OmniSharp
            // is actually running under the installed Mono.
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

            // Double-check Mono version. MSBuild support in versions earlier than 5.2.0 is
            // too experimental to use.
            var monoVersion = PlatformHelper.GetMonoVersion();
            if (monoVersion == null)
            {
                Logger.LogDebug("Could not retrieve Mono version");
                return NoInstances;
            }

            if (monoVersion < new Version("5.2.0"))
            {
                Logger.LogDebug($"Found Mono MSBuild but it could not be used because it is version {monoVersion} and in needs to be >= 5.2.0");
                return NoInstances;
            }

            var toolsPath = Path.Combine(path, "15.0", "bin");
            if (!Directory.Exists(toolsPath))
            {
                Logger.LogDebug($"Mono MSBuild could not be used because '{toolsPath}' does not exist.");
                return NoInstances;
            }

            var propertyOverrides = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

            var localMSBuildPath = FindLocalMSBuildDirectory();
            if (localMSBuildPath != null)
            {
                var localRoslynPath = Path.Combine(localMSBuildPath, "15.0", "Bin", "Roslyn");
                propertyOverrides.Add("CscToolPath", localRoslynPath);
                propertyOverrides.Add("CscToolExe", "csc.exe");
            }

            return ImmutableArray.Create(
                new MSBuildInstance(
                    nameof(DiscoveryType.Mono),
                    toolsPath,
                    new Version(15, 0),
                    DiscoveryType.Mono,
                    propertyOverrides.ToImmutable()));
        }
    }
}
