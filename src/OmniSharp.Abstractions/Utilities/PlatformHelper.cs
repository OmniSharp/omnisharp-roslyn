using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OmniSharp.Utilities
{
    public static class PlatformHelper
    {
        private static Lazy<bool> s_isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);
        private static Lazy<string> s_monoRuntimePath = new Lazy<string>(FindMonoRuntimePath);
        private static Lazy<string> s_monoXBuildFrameworksDirPath = new Lazy<string>(FindMonoXBuildFrameworksDirPath);

        public static bool IsMono => s_isMono.Value;
        public static string MonoRuntimePath => s_monoRuntimePath.Value;
        public static string MonoXBuildFrameworksDirPath => s_monoXBuildFrameworksDirPath.Value;

        public static bool IsWindows => Path.DirectorySeparatorChar == '\\';

        private static IEnumerable<string> s_searchPaths;

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

        private static string FindMonoRuntimePath()
        {
            if (IsWindows)
            {
                return null;
            }

            var monoPath = GetSearchPaths()
                .Select(p => Path.Combine(p, "mono"))
                .FirstOrDefault(File.Exists);

            if (monoPath == null)
            {
                return null;
            }

            return RealPath(monoPath);
        }

        private static string FindMonoXBuildFrameworksDirPath()
        {
            if (IsWindows)
            {
                return null;
            }

            const string defaultXBuildFrameworksDirPath = "/usr/lib/mono/xbuild-frameworks";
            if (Directory.Exists(defaultXBuildFrameworksDirPath))
            {
                return defaultXBuildFrameworksDirPath;
            }

            // The normal Unix path doesn't exist, so we'll fallback to finding Mono using the
            // runtime location. This is the likely situation on macOS.
            var monoRuntimePath = MonoRuntimePath;
            if (string.IsNullOrEmpty(monoRuntimePath))
            {
                return null;
            }

            // mono should be located within a directory that is a sibling to the lib directory.
            var monoDirPath = Path.GetDirectoryName(monoRuntimePath);

            // The base directory is one folder up
            var monoBaseDirPath = Path.Combine(monoDirPath, "..");
            monoBaseDirPath = Path.GetFullPath(monoBaseDirPath);

            // We expect the xbuild-frameworks to be in /Versions/Current/lib/mono/xbuild-frameworks.
            var monoXBuildFrameworksDirPath = Path.Combine(monoBaseDirPath, "lib", "mono", "xbuild-frameworks");
            monoXBuildFrameworksDirPath = Path.GetFullPath(monoXBuildFrameworksDirPath);

            return Directory.Exists(monoXBuildFrameworksDirPath)
                ? monoXBuildFrameworksDirPath
                : null;
        }
    }
}
