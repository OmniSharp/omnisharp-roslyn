using System.Collections.Generic;

namespace OmniSharp.Plugins
{
    public class PluginConfig
    {
        public string Name { get; set; }
        public string[] Endpoints { get; }
        public string Language { get; set; }
        public IEnumerable<string> Extensions { get; set; }
    }
}
