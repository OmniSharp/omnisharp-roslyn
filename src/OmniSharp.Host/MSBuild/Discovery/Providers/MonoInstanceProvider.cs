using System;
using System.Collections.Immutable;
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

            var path = PlatformHelper.GetMonoMSBuildDirPath();
            if (path == null)
            {
                return NoInstances;
            }

            // Double-check Mono version. MSBuild support in versions earlier than 5.2.0 is
            // too experimental to use.
            var monoVersion = PlatformHelper.GetMonoVersion();
            if (monoVersion == null || monoVersion < new Version("5.2.0"))
            {
                return NoInstances;
            }

            var toolsPath = Path.Combine(path, "15.0", "bin");
            if (!Directory.Exists(toolsPath))
            {
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
