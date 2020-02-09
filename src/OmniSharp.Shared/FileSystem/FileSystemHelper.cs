using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using OmniSharp.Options;

namespace OmniSharp.FileSystem
{
    [Export, Shared]
    public class FileSystemHelper
    {
        private static string s_directorySeparatorChar = Path.DirectorySeparatorChar.ToString();
        private readonly OmniSharpOptions _omniSharpOptions;
        private readonly IOmniSharpEnvironment _omniSharpEnvironment;

        [ImportingConstructor]
        public FileSystemHelper(OmniSharpOptions omniSharpOptions, IOmniSharpEnvironment omniSharpEnvironment)
        {
            _omniSharpOptions = omniSharpOptions;
            _omniSharpEnvironment = omniSharpEnvironment;
        }

        public IEnumerable<string> GetFiles(string includePattern) => GetFiles(includePattern, _omniSharpEnvironment.TargetDirectory);

        public IEnumerable<string> GetFiles(string includePattern, string targetDirectory)
        {
            var matcher = new Matcher();
            matcher.AddInclude(includePattern);

            if (_omniSharpOptions.FileOptions.SystemExcludeSearchPatterns != null && _omniSharpOptions.FileOptions.SystemExcludeSearchPatterns.Any())
            {
                matcher.AddExcludePatterns(_omniSharpOptions.FileOptions.SystemExcludeSearchPatterns);
            }

            if (_omniSharpOptions.FileOptions.ExcludeSearchPatterns != null && _omniSharpOptions.FileOptions.ExcludeSearchPatterns.Any())
            {
                matcher.AddExcludePatterns(_omniSharpOptions.FileOptions.ExcludeSearchPatterns);
            }

            return matcher.GetResultsInFullPath(targetDirectory);
        }

        public static string GetRelativePath(string fullPath, string basePath)
        {
            // if any of them is not set, abort
            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(fullPath)) return null;

            // paths must be rooted
            if (!Path.IsPathRooted(basePath) || !Path.IsPathRooted(fullPath)) return null;

            // if they are the same, abort
            if (fullPath.Equals(basePath, StringComparison.Ordinal)) return null;

            if (!Path.HasExtension(basePath) && !basePath.EndsWith(s_directorySeparatorChar))
            {
                basePath += Path.DirectorySeparatorChar;
            }

            var baseUri = new Uri(basePath);
            var fullUri = new Uri(fullPath);
            var relativeUri = baseUri.MakeRelativeUri(fullUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return relativePath;
        }

        /// <summary>
        /// Verifies whether the path is excluded by any of the given patterns
        /// </summary>
        /// <param name="path">The path that will be verified</param>
        /// <param name="targetDirectory">The target directory</param>
        /// <param name="excludePatterns">A list of patterns to be excluded</param>
        public static bool IsPathExcluded(
            string filePath,
            string targetDirectory,
            IEnumerable<string> excludePatterns)
        {
            if (!IsChildPath(filePath, targetDirectory))
            {
                return false;
            }

            filePath = Path.GetFullPath(filePath);
            targetDirectory = Path.GetFullPath(targetDirectory);

            var targetDirectoryLength = targetDirectory.Length;

            // Checks whether the target directory has a leading path separator.
            // If it does not, one more char should be accounted in its length
            if (!targetDirectory.EndsWith(s_directorySeparatorChar, StringComparison.InvariantCulture))
            {
                targetDirectoryLength += 1;
            }

            // Removes the target directory part of the file to get the relative path
            var relativeFilePath = filePath.Substring(
                targetDirectoryLength,
                filePath.Length - targetDirectoryLength);

            var matcher = new Matcher();
            matcher.AddInclude(relativeFilePath);

            if (excludePatterns != null)
            {
                matcher.AddExcludePatterns(excludePatterns);
            }

            return !matcher.GetResultsInFullPath(targetDirectory).Any();
        }

        /// <summary>
        /// Verifies whether the path specified is a child of the specified parent path
        /// </summary>
        /// <param name="path">Path that will be verified</param>
        /// <param name="parentPath">The possible parent path</param>
        public static bool IsChildPath(string path, string parentPath)
        {
            if (path == null || parentPath == null)
            {
                return false;
            }

            path = Path.GetFullPath(path);
            parentPath = Path.GetFullPath(parentPath);

            if (path.Length < parentPath.Length)
            {
                return false;
            }

            return string.Equals(
                path.Substring(0, parentPath.Length),
                parentPath,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Find the first occurrence of a set of candidate paths that is the parent
        /// of the specified path
        /// </summary>
        /// <param name="path">The path that will be verified</param>
        /// <param name="candidateParentPaths">A set of possible parent paths</param>
        public static string FindParentPath(
            string path,
            IEnumerable<string> candidateParentPaths)
        {
            if (path == null || candidateParentPaths == null)
            {
                return null;
            }

            return candidateParentPaths.FirstOrDefault(
                parentPath => IsChildPath(
                    path,
                    Path.HasExtension(parentPath)
                        ? Path.GetDirectoryName(parentPath)
                        : parentPath));
        }
    }
}
