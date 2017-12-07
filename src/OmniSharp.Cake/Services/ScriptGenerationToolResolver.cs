using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OmniSharp.Cake.Configuration;
using OmniSharp.Utilities;

namespace OmniSharp.Cake.Services
{
    internal static class ScriptGenerationToolResolver
    {
        public static string GetExecutablePath(string rootPath, ICakeConfiguration configuration, CakeOptions options)
        {
            // First check if registered through OmniSharp options
            var executablepath = options.BakeryPath;
            if (!string.IsNullOrEmpty(executablepath) && File.Exists(executablepath))
            {
                return executablepath;
            }

            // First check if installed in workspace
            executablepath = ResolveFromToolFolder(rootPath, configuration);
            if (!string.IsNullOrEmpty(executablepath))
            {
                return executablepath;
            }

            // If not check from path
            return ResolveFromPath();
        }

        private static string ResolveFromToolFolder(string rootPath, ICakeConfiguration configuration)
        {
            var toolPath = GetToolPath(rootPath, configuration);

            if (!Directory.Exists(toolPath))
            {
                return string.Empty;
            }

            var bakeryPath = GetLatestBakeryPath(toolPath);

            if (bakeryPath == null)
            {
                return string.Empty;
            }

            return Path.Combine(toolPath, bakeryPath, "tools", "Cake.Bakery.exe");
        }

        private static string GetToolPath(string rootPath, ICakeConfiguration configuration)
        {
            var toolPath = configuration.GetValue(Constants.Paths.Tools);
            return Path.Combine(rootPath, !string.IsNullOrWhiteSpace(toolPath) ? toolPath : "tools");
        }

        private static string GetLatestBakeryPath(string toolPath)
        {
            var directories = GetBakeryPaths(toolPath);

            // TODO: Sort by semantic version?
            return directories.OrderByDescending(x => x).FirstOrDefault();
        }

        private static IEnumerable<string> GetBakeryPaths(string toolPath)
        {
            foreach (var directory in Directory.EnumerateDirectories(toolPath))
            {
                var topDirectory = directory.Split(Path.DirectorySeparatorChar).Last();
                if (topDirectory.StartsWith("cake.bakery", StringComparison.OrdinalIgnoreCase))
                {
                    yield return topDirectory;
                }
            }
        }

        private static string ResolveFromPath()
        {
            foreach (var searchPath in PlatformHelper.GetSearchPaths())
            {
                var path = Path.Combine(searchPath, "Cake.Bakery.exe");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}
