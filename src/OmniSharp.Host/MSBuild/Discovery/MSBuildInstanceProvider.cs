using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild.Discovery
{
    internal abstract class MSBuildInstanceProvider
    {
        protected readonly ILogger Logger;
        protected static readonly ImmutableArray<MSBuildInstance> NoInstances = ImmutableArray<MSBuildInstance>.Empty;

        protected MSBuildInstanceProvider(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger(this.GetType());
        }

        public abstract ImmutableArray<MSBuildInstance> GetInstances();

        protected static string FindLocalMSBuildDirectory()
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
    }
}
