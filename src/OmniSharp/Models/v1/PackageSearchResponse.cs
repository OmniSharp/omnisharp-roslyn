using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class PackageSearchResponse
    {
        public IEnumerable<PackageSearchItem> Items {get;set;}
    }

    public class PackageSearchItem
    {
        public string Name {get;set;}
    }
}
