using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class PackageSearchResponse
    {
        public IEnumerable<PackageSearchItem> Packages { get; set; }
        public IEnumerable<string> Sources { get; set; }
    }
}
