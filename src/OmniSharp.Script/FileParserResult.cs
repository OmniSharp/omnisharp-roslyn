using System.Collections.Generic;

namespace OmniSharp.Script
{
    public class FileParserResult
    {
        public FileParserResult()
        {
            Namespaces = new HashSet<string>();
            References = new HashSet<string>();
            LoadedScripts = new HashSet<string>();
        }

        public HashSet<string> Namespaces { get; }

        public HashSet<string> References { get; }

        public HashSet<string> LoadedScripts { get; }
    }
}