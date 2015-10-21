using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class PackageSearchItem
    {
        public string Id { get; set; }
        public bool HasVersion { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
    }
}
