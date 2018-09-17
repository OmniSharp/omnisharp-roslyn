using System.Collections.Generic;

namespace OmniSharp.ConfigurationManager
{
    public class OmniSharpConfiguration
    {
        public OmniSharpConfiguration()
        {
        }

        public TestCommands TestCommands { get; set; }
        public string ConfigFileLocation { get; set; }
        public BuildPath MSBuildPath { get; set; }
        public IEnumerable<PathReplacement> PathReplacements { get; set; }

        public class PathReplacement
        {
            public string From { get; set; }
            public string To { get; set; }
        }
    }
}
