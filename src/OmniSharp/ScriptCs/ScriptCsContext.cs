using System.Collections.Generic;

namespace OmniSharp.ScriptCs
{
    public class ScriptCsContext
    {
        public HashSet<string> CsxFiles { get; } = new HashSet<string>();
        public HashSet<string> References { get; } = new HashSet<string>();
        public HashSet<string> Usings { get; } = new HashSet<string>();
        public HashSet<string> ScriptPacks { get; } = new HashSet<string>();

        public string Path { get; set; }
    }
}
