using System.Collections.Generic;

namespace OmniSharp.Models.Events
{
    public class UnresolvedDependenciesMessage
    {
        public string FileName { get; set; }

        public IEnumerable<PackageDependency> UnresolvedDependencies { get; set; }
    }
}