using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OmniSharp.Options
{
    public class RoslynExtensionsOptions : OmniSharpExtensionsOptions
    {
        public bool EnableDecompilationSupport { get; set; }
        public bool EnableAnalyzersSupport { get; set; }
        public bool EnableImportCompletion { get; set; }
        public bool EnableAsyncCompletion { get; set; }
        public int DocumentAnalysisTimeoutMs { get; set; } = 10 * 1000;
    }

    public class OmniSharpExtensionsOptions
    {
        public string[] LocationPaths { get; set; }

        public IEnumerable<string> GetNormalizedLocationPaths(IOmniSharpEnvironment env)
        {
            if (LocationPaths == null || LocationPaths.Length == 0) return Enumerable.Empty<string>();

            var normalizePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var locationPath in LocationPaths)
            {
                if (Path.IsPathRooted(locationPath))
                {
                    normalizePaths.Add(locationPath);
                }
                else
                {
                    normalizePaths.Add(Path.Combine(env.TargetDirectory, locationPath));
                }
            }

            return normalizePaths;
        }
    }
}
