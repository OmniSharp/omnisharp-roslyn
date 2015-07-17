using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class PackageSearchResponse
    {
        public IEnumerable<PackageSearchItem> Items { get; set; }
        public IEnumerable<string> Sources { get; set; }
    }
}
