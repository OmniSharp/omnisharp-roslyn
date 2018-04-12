using Microsoft.Extensions.FileSystemGlobbing;
using OmniSharp.Options;
using System.Collections.Generic;
using System.Composition;
using System.Linq;

namespace OmniSharp.FileSystem
{
    [Export, Shared]
    public class FileSystemHelper
    {
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

            if (_omniSharpOptions.FileOptions.ExcludeSearchPatterns != null && _omniSharpOptions.FileOptions.ExcludeSearchPatterns.Any())
            {
                matcher.AddExcludePatterns(_omniSharpOptions.FileOptions.ExcludeSearchPatterns);
            }

            return matcher.GetResultsInFullPath(targetDirectory);
        }
    }
}
