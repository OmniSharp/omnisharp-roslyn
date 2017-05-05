using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OmniSharp.Options
{
    public class CodeActionOptions
    {
        public string[] LocationPaths { get; set; }

        public IEnumerable<string> GetLocations(IOmniSharpEnvironment env)
        {
            if (LocationPaths == null) return Enumerable.Empty<string>();

            var normalizePaths = new HashSet<string>();
            foreach (var locationPath in LocationPaths)
            {
                if (Path.IsPathRooted(locationPath))
                {
                    normalizePaths.Add(locationPath);
                }

                normalizePaths.Add(Path.Combine(env.TargetDirectory, locationPath));
            }

            return normalizePaths;
        }
    }
}
