using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OmniSharp.Utilities
{
    public static class PlatformHelper
    {
        private static IEnumerable<string> s_searchPaths;
        private static string s_monoRuntimePath;
        private static string s_monoLibDirPath;

        public static bool IsMono => Type.GetType("Mono.Runtime") != null;
        public static bool IsWindows => Path.DirectorySeparatorChar == '\\';

        public static IEnumerable<string> GetSearchPaths()
        {
            if (s_searchPaths == null)
            {
                var path = Environment.GetEnvironmentVariable("PATH");
                if (path == null)
                {
                    return Array.Empty<string>();
                }

                s_searchPaths = path
                    .Split(Path.PathSeparator)
                    .Select(p => p.Trim('"'));
            }

            return s_searchPaths;
        }

        // http://man7.org/linux/man-pages/man3/realpath.3.html
        // CharSet.Ansi is UTF8 on Unix
        [DllImport("libc", EntryPoint = "realpath", CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Unix_realpath(string path, IntPtr buffer);

        // http://man7.org/linux/man-pages/man3/free.3.html
        [DllImport("libc", EntryPoint = "free", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Unix_free(IntPtr ptr);

        /// <summary>
        /// Returns the conanicalized absolute path from a given path, expanding symbolic links and resolving
        /// references to /./, /../ and extra '/' path characters.
        /// </summary>
        private static string RealPath(string path)
        {
            if (IsWindows)
            {
                throw new PlatformNotSupportedException($"{nameof(RealPath)} can only be called on Unix.");
            }

            var ptr = Unix_realpath(path, IntPtr.Zero);
            var result = Marshal.PtrToStringAnsi(ptr); // uses UTF8 on Unix
            Unix_free(ptr);

            return result;
        }

        public static Version GetMonoVersion()
        {
            var output = ProcessHelper.RunAndCaptureOutput("mono", "--version");
            if (output == null)
            {
                return null;
            }

            // The mono --version text contains several lines. We'll just walk through the first line,
            // word by word, until we find a word that parses as a version number. Normally, this should
            // be the *fifth* word. E.g. "Mono JIT compiler version 4.8.0"

            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var words = lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                if (Version.TryParse(word, out var version))
                {
                    return version;
                }
            }

            return null;
        }

        public static string GetMonoRuntimePath()
        {
            if (IsWindows)
            {
                return null;
            }

            if (s_monoRuntimePath == null)
            {
                var monoPath = GetSearchPaths()
                    .Select(p => Path.Combine(p, "mono"))
                    .FirstOrDefault(File.Exists);

                if (monoPath == null)
                {
                    return null;
                }

                s_monoRuntimePath = RealPath(monoPath);
            }

            return s_monoRuntimePath;
        }

        public static string GetMonoLibDirPath()
        {
            if (IsWindows)
            {
                return null;
            }

            const string DefaultMonoLibPath = "/usr/lib/mono";
            if (Directory.Exists(DefaultMonoLibPath))
            {
                return DefaultMonoLibPath;
            }

            // The normal Unix path doesn't exist, so we'll fallback to finding Mono using the
            // runtime location. This is the likely situation on macOS.

            if (s_monoLibDirPath == null)
            {
                var monoRuntimePath = GetMonoRuntimePath();
                if (monoRuntimePath == null)
                {
                    return null;
                }

                var monoDirPath = Path.GetDirectoryName(monoRuntimePath);

                var monoLibDirPath = Path.Combine(monoDirPath, "..", "lib", "mono");
                monoLibDirPath = Path.GetFullPath(monoLibDirPath);

                s_monoLibDirPath = Directory.Exists(monoLibDirPath)
                    ? monoLibDirPath
                    : null;
            }

            return s_monoLibDirPath;
        }

        public static string GetMonoMSBuildDirPath()
        {
            if (IsWindows)
            {
                return null;
            }

            var monoLibDirPath = GetMonoLibDirPath();
            if (monoLibDirPath == null)
            {
                return null;
            }

            var monoMSBuildDirPath = Path.Combine(monoLibDirPath, "msbuild");
            monoMSBuildDirPath = Path.GetFullPath(monoMSBuildDirPath);

            return Directory.Exists(monoMSBuildDirPath)
                ? monoMSBuildDirPath
                : null;
        }

        public static string GetMonoXBuildDirPath()
        {
            if (IsWindows)
            {
                return null;
            }

            const string DefaultMonoXBuildDirPath = "/usr/lib/mono/xbuild";
            if (Directory.Exists(DefaultMonoXBuildDirPath))
            {
                return DefaultMonoXBuildDirPath;
            }

            var monoLibDirPath = GetMonoLibDirPath();
            if (monoLibDirPath == null)
            {
                return null;
            }

            var monoXBuildDirPath = Path.Combine(monoLibDirPath, "xbuild");
            monoXBuildDirPath = Path.GetFullPath(monoXBuildDirPath);

            return Directory.Exists(monoXBuildDirPath)
                ? monoXBuildDirPath
                : null;
        }

        public static string GetMonoXBuildFrameworksDirPath()
        {
            if (IsWindows)
            {
                return null;
            }

            const string DefaultMonoXBuildFrameworksDirPath = "/usr/lib/mono/xbuild-frameworks";
            if (Directory.Exists(DefaultMonoXBuildFrameworksDirPath))
            {
                return DefaultMonoXBuildFrameworksDirPath;
            }

            var monoLibDirPath = GetMonoLibDirPath();
            if (monoLibDirPath == null)
            {
                return null;
            }

            var monoXBuildFrameworksDirPath = Path.Combine(monoLibDirPath, "xbuild-frameworks");
            monoXBuildFrameworksDirPath = Path.GetFullPath(monoXBuildFrameworksDirPath);

            return Directory.Exists(monoXBuildFrameworksDirPath)
                ? monoXBuildFrameworksDirPath
                : null;
        }
    }
}
