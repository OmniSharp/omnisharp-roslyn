using System.Collections.Generic;
using System.Linq;
using OmniSharp.Roslyn.Models;

namespace OmniSharp.Script
{
    public class ScriptContextModel
    {
        public ScriptContextModel(ScriptContext context)
        {
            RootPath = context.RootPath;
            CsxFilesBeingProcessed = context.CsxFilesBeingProcessed;
            CsxFileProjects = context.CsxFileProjects.ToDictionary(x => x.Key, x => new ProjectInfoModel(x.Value));
            CsxReferences = context.CsxReferences.ToDictionary(x => x.Key, x => x.Value.Select(z => new ReferenceModel(z)));
            CsxLoadReferences = context.CsxLoadReferences;
            CsxUsings = context.CsxUsings.ToDictionary(x => x.Key, x => x.Value.AsEnumerable());
            CommonReferences = context.CommonReferences.Select(z => new ReferenceModel(z));
            CommonUsings = context.CommonUsings;
        }

        public IEnumerable<string> CsxFilesBeingProcessed { get; }

        // All of the followings are keyed with the file path
        // Each .csx file is wrapped into a project
        public Dictionary<string, ProjectInfoModel> CsxFileProjects { get; }
        public Dictionary<string, IEnumerable<ReferenceModel>> CsxReferences { get; }
        public Dictionary<string, List<string>> CsxLoadReferences { get; }
        public Dictionary<string, IEnumerable<string>> CsxUsings { get; }
        public IEnumerable<ReferenceModel> CommonReferences { get; }
        public IEnumerable<string> CommonUsings { get; }
        public string RootPath { get; set; }
    }
}
