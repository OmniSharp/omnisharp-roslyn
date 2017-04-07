using System;
using System.Collections.Generic;
using System.IO;

namespace OmniSharp.Utilities
{
    public static class PlatformHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);
        private static Lazy<string> _monoPath = new Lazy<string>(FindMonoPath);
        private static Lazy<string> _monoXBuildFrameworksDirPath = new Lazy<string>(FindMonoXBuildFrameworksDirPath);

        public static bool IsMono => _isMono.Value;
        public static string MonoFilePath => _monoPath.Value;
        public static string MonoXBuildFrameworksDirPath => _monoXBuildFrameworksDirPath.Value;

        public static bool IsWindows => Path.DirectorySeparatorChar == '\\';

        private static string FindMonoPath()
        {
            // To locate Mono on unix, we use the 'which' command (https://en.wikipedia.org/wiki/Which_(Unix))
            var monoFilePath = RunOnBashAndCaptureOutput("which", "mono");

            if (string.IsNullOrEmpty(monoFilePath))
            {
                return null;
            }

            Console.WriteLine($"Discovered Mono file path: {monoFilePath}");

            // 'mono' is likely a symbolic link. Try to resolve it.
            var resolvedMonoFilePath = ResolveSymbolicLink(monoFilePath);

            if (StringComparer.OrdinalIgnoreCase.Compare(monoFilePath, resolvedMonoFilePath) == 0)
            {
                return monoFilePath;
            }

            Console.WriteLine($"Resolved symbolic link for Mono file path: {resolvedMonoFilePath}");

            return resolvedMonoFilePath;
        }

        private static string FindMonoXBuildFrameworksDirPath()
        {
            const string defaultXBuildFrameworksDirPath = "/usr/lib/mono/xbuild-frameworks";
            if (Directory.Exists(defaultXBuildFrameworksDirPath))
            {
                return defaultXBuildFrameworksDirPath;
            }

            // The normal Unix path doesn't exist, so we'll fallback to finding Mono using the
            // runtime location. This is the likely situation on macOS.
            var monoFilePath = MonoFilePath;
            if (string.IsNullOrEmpty(monoFilePath))
            {
                return null;
            }

            // mono should be located within a directory that is a sibling to the lib directory.
            var monoDirPath = Path.GetDirectoryName(monoFilePath);

            // The base directory is one folder up
            var monoBaseDirPath = Path.Combine(monoDirPath, "..");
            monoBaseDirPath = Path.GetFullPath(monoBaseDirPath);

            // We expect the xbuild-frameworks to be in /Versions/Current/lib/mono/xbuild-frameworks.
            var monoXBuildFrameworksDirPath = Path.Combine(monoBaseDirPath, "lib/mono/xbuild-frameworks");
            monoXBuildFrameworksDirPath = Path.GetFullPath(monoXBuildFrameworksDirPath);

            return Directory.Exists(monoXBuildFrameworksDirPath)
                ? monoXBuildFrameworksDirPath
                : null;
        }

        private static string ResolveSymbolicLink(string path)
        {
            var result = ResolveSymbolicLink(new List<string> { path });

            return CanonicalizePath(result);
        }

        private static string ResolveSymbolicLink(List<string> paths)
        {
            while (!HasCycle(paths))
            {
                // We use 'readlink' to resolve symbolic links on unix. Note that OSX does not
                // support the -f flag for recursively resolving symbolic links and canonicalzing
                // the final path.

                var originalPath = paths[paths.Count - 1];
                var newPath = RunOnBashAndCaptureOutput("readlink", $"{originalPath}");

                if (string.IsNullOrEmpty(newPath) ||
                    string.CompareOrdinal(originalPath, newPath) == 0)
                {
                    return originalPath;
                }

                if (!newPath.StartsWith("/"))
                {
                    var dir = File.Exists(originalPath)
                        ? Path.GetDirectoryName(originalPath)
                        : originalPath;

                    newPath = Path.Combine(dir, newPath);
                    newPath = Path.GetFullPath(newPath);
                }

                paths.Add(newPath);
            }

            return null;
        }

        private static bool HasCycle(List<string> paths)
        {
            var target = paths[0];
            for (var i = 1; i < paths.Count; i++)
            {
                var path = paths[i];

                if (string.CompareOrdinal(target, path) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string CanonicalizePath(string path)
        {
            if (File.Exists(path))
            {
                return Path.Combine(
                    CanonicalizeDirectory(Path.GetDirectoryName(path)),
                    Path.GetFileName(path));
            }
            else
            {
                return CanonicalizeDirectory(path);
            }
        }

        private static string CanonicalizeDirectory(string directoryName)
        {
            // Use "pwd -P" to get the directory name with all symbolic links on Unix.
            return RunOnBashAndCaptureOutput("pwd", "-P", directoryName);
        }

        private static string RunOnBashAndCaptureOutput(string fileName, string arguments, string workingDirectory = null)
        {
            return ProcessHelper.RunAndCaptureOutput("/bin/bash", $"-c '{fileName} {arguments}'", workingDirectory);
        }
    }
}
