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
    }
}
