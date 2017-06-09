using System.Collections.Generic;

namespace OmniSharp.Models.PackageSearch
{
    public class PackageSearchResponse
    {
        public IEnumerable<PackageSearchItem> Packages { get; set; }
    }
}
