using System.Collections.Generic;

namespace OmniSharp.Models.PackageVersion
{
    public class PackageVersionResponse
    {
        public IEnumerable<string> Versions { get; set; }
    }
}
