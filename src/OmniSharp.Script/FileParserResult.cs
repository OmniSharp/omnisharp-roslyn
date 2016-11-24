using System.Collections.Generic;

namespace OmniSharp.Script
{
    public class FileParserResult
    {
        public HashSet<string> Namespaces { get; } = new HashSet<string>();
        public HashSet<string> References { get; } = new HashSet<string>();
        public HashSet<string> LoadedScripts { get; } = new HashSet<string>();
    }
}