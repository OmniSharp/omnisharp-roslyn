using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class UnresolvedDependenciesMessage
    {
        public string FileName { get; set; }

        public IEnumerable<PackageDependency> UnresolvedDependencies { get; set; }
    }
}