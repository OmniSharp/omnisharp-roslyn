using System.Collections.Generic;

namespace OmniSharp.Script
{
    public class ScriptContextModelCollection
    {
        public ScriptContextModelCollection(IEnumerable<ScriptContextModel> projects)
        {
            Projects = projects;
        }

        public IEnumerable<ScriptContextModel> Projects { get; }
    }
}