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
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var path = PlatformHelper.GetMonoMSBuildDirPath();
            if (path == null)
            {
                return ImmutableArray<MSBuildInstance>.Empty;
            }

            var toolsPath = Path.Combine(path, "15.0", "bin");
            var roslynPath = Path.Combine(toolsPath, "Roslyn");

            var propertyOverrides = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
            propertyOverrides.Add("CscToolPath", roslynPath);
            propertyOverrides.Add("CscToolExe", "csc.exe");

            return ImmutableArray.Create(
                new MSBuildInstance(
                    "Mono",
                    toolsPath,
                    new Version(15, 0),
                    DiscoveryType.Mono,
                    propertyOverrides.ToImmutable()));
        }
    }
}
