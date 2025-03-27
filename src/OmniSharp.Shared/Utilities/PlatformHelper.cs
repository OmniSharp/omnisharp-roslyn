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
    }
}
