using System.IO;

namespace OmniSharp.Script
{
    public class ScriptOptions
    {
        public bool EnableScriptNuGetReferences { get; set; }

        public string DefaultTargetFramework { get; set; } = "net46";
    }
}
