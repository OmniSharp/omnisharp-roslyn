using System;
using System.Diagnostics;
using System.IO;

namespace OmniSharp
{
    public static class PlatformHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);
        private static Lazy<string> _monoPath = new Lazy<string>(FindMonoPath);
        private static Lazy<string> _monoXBuildFrameworksDirPath = new Lazy<string>(FindMonoXBuildFrameworksDirPath);

        public static bool IsMono => _isMono.Value;
        public static string MonoFilePath => _monoPath.Value;
        public static string MonoXBuildFrameworksDirPath => _monoXBuildFrameworksDirPath.Value;

        private static string FindMonoPath()
        {
            // To locate Mono on unix, we use the 'which' command (https://en.wikipedia.org/wiki/Which_(Unix))
            var monoFilePath = LaunchProcessAndCaptureOutput("which", "mono");

            return monoFilePath;
        }

        private static string FindMonoXBuildFrameworksDirPath()
        {
            var monoFilePath = MonoFilePath;
            if (monoFilePath == null)
            {
                return null;
            }

            Console.WriteLine($"Mono file path: {monoFilePath}");

            // mono should be located in the '/Versions/Current/Commands' directory.
            var monoCommandsDirPath = Path.GetDirectoryName(monoFilePath);

            // The base directory is one folder up
            var monoBaseDirPath = Path.Combine(monoCommandsDirPath, "..");
            monoBaseDirPath = Path.GetFullPath(monoBaseDirPath);

            // It's likely that this is a symbolic link to a specific version, so try to resolve it.
            monoBaseDirPath = ResolveSymbolicLink(monoBaseDirPath);

            // We expect the xbuild-frameworks to be in /Versions/Current/lib/mono/xbuild-frameworks.
            var monoXBuildFrameworksDirPath = Path.Combine(monoBaseDirPath, "lib/mono/xbuild-frameworks");
            monoXBuildFrameworksDirPath = Path.GetFullPath(monoXBuildFrameworksDirPath);

            return Directory.Exists(monoXBuildFrameworksDirPath)
                ? monoXBuildFrameworksDirPath
                : null;
        }

        private static string ResolveSymbolicLink(string filePath)
        {
            // We use 'readlink' to resolve symbol links on unix. Note that OSX does not support the -f flag
            // for canonicalization.
            var result = LaunchProcessAndCaptureOutput("readlink", $"{filePath}");

            return !string.IsNullOrEmpty(result)
                ? result
                : filePath;
        }

        private static string LaunchProcessAndCaptureOutput(string command, string args)
        {
            var startInfo = new ProcessStartInfo(command, args);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;

            var process = new Process();
            process.StartInfo = startInfo;

            try
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Trim();
            }
            catch
            {
                Console.WriteLine($"Failed to launch '{command}' with args, '{args}'");
                return null;
            }
        }
    }
}
