using System.Collections.Generic;

namespace OmniSharp.Plugins
{
    public class OopConfig
    {
        public string Name { get; set; }
        public string[] Endpoints { get; }
        public string Language { get; set; }
        public IEnumerable<string> Extensions { get; set; }
    }
}
