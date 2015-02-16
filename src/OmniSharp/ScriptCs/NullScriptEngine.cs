#if ASPNET50
using ScriptCs.Contracts;
using System.Collections.Generic;

namespace OmniSharp.ScriptCs
{
    public class NullScriptEngine : IScriptEngine
    {
        public string BaseDirectory { get; set; }

        public string CacheDirectory { get; set; }

        public string FileName { get; set; }

        public ScriptResult Execute(string code, string[] scriptArgs, AssemblyReferences references, IEnumerable<string> namespaces, ScriptPackSession scriptPackSession)
        {
            return new ScriptResult();
        }
    }
}
#endif