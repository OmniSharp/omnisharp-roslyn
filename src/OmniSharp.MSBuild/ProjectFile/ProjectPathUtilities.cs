using System;
using System.IO;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal static class ProjectPathUtilities
    {
        public static string GetFullPath(string path, string basePath)
            => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));

        public static string GetRelativePath(string basePath, string path)
        {
            var fullBasePath = Path.GetFullPath(basePath);
            var fullPath = Path.GetFullPath(path);
            if (string.Equals(fullBasePath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return ".";
            }

            if (!fullBasePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullBasePath += Path.DirectorySeparatorChar;
            }

            return Uri.UnescapeDataString(new Uri(fullBasePath).MakeRelativeUri(new Uri(fullPath)).ToString())
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }
}
