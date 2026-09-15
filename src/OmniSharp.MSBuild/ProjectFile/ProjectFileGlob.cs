using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal sealed class ProjectFileGlob
    {
        private readonly Matcher _matcher;

        public ProjectFileGlob(
            string projectDirectory,
            IEnumerable<string> includes,
            IEnumerable<string> excludes,
            IEnumerable<string> removes)
        {
            _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            _matcher.AddIncludePatterns(includes.Select(pattern => NormalizePattern(projectDirectory, pattern)));
            _matcher.AddExcludePatterns(excludes.Concat(removes)
                .Select(pattern => NormalizePattern(projectDirectory, pattern)));
        }

        public bool IsMatch(string path)
            => _matcher.Match(path.Replace('\\', '/')).HasMatches;

        private static string NormalizePattern(string projectDirectory, string pattern)
        {
            var normalizedPattern = Path.IsPathRooted(pattern)
                ? Path.GetRelativePath(projectDirectory, pattern)
                : pattern;
            normalizedPattern = normalizedPattern.Replace('\\', '/');

            while (normalizedPattern.Contains("//", StringComparison.Ordinal))
            {
                normalizedPattern = normalizedPattern.Replace("//", "/", StringComparison.Ordinal);
            }

            return normalizedPattern;
        }
    }
}
