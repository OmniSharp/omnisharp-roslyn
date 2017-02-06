using System.Collections.Generic;

namespace OmniSharp.Script
{
    public class ScriptContextModel
    {
        public IEnumerable<ScriptContext> ScriptContexts { get; set; }

        public ScriptContextModel(IEnumerable<ScriptContext> scriptContexts)
        {
            ScriptContexts = scriptContexts;
        }
    }
}
