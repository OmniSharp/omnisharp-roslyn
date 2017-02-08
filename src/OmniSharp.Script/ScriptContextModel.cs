using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.Models;

namespace OmniSharp.Script
{
    public class ScriptContextModel
    {
        public ScriptContextModel(string csxPath, ProjectInfo project)
        {
            CsxPath = csxPath;
            ProjectModel = new ProjectInfoModel(project);
            CommonUsings = ScriptProjectSystem.DefaultNamespaces;
        }

        public string CsxPath { get; set; }
        public ProjectInfoModel ProjectModel { get; set; }
        public IEnumerable<string> CommonUsings { get; }
    }
}