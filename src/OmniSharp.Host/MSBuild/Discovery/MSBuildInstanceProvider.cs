using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// Handles locating the MSBuild tools path given a base path (typically a Visual Studio install path).
        /// </summary>
        protected string FindMSBuildToolsPath(string basePath)
        {
            if (TryGetToolsPath("Current", "Bin", out var result) ||
                TryGetToolsPath("Current", "bin", out result) ||
                TryGetToolsPath("15.0", "Bin", out result) ||
                TryGetToolsPath("15.0", "bin", out result))
            {
                return result;
            }

            Logger.LogDebug($"Could not locate MSBuild tools path within {basePath}");
            return null;

            bool TryGetToolsPath(string versionPath, string binPath, out string toolsPath)
            {
                toolsPath = null;

                var baseDir = new DirectoryInfo(basePath);
                if (!baseDir.Exists)
                {
                    return false;
                }

                var versionDir = baseDir.EnumerateDirectories().FirstOrDefault(di => di.Name == versionPath);
                if (versionDir == null)
                {
                    return false;
                }

                var binDir = versionDir.EnumerateDirectories().FirstOrDefault(di => di.Name == binPath);
                if (binDir == null)
                {
                    return false;
                }

                toolsPath = binDir.FullName;
                return true;
            }
        }

        protected static string FindLocalMSBuildDirectory()
        {
            // If OmniSharp is running normally, MSBuild is located in an '.msbuild' folder beneath OmniSharp.exe.
            var msbuildDirectory = Path.Combine(AppContext.BaseDirectory, ".msbuild");

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
