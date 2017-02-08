using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.Models;

namespace OmniSharp.Script
{
    public class ScriptContextModel
    {
        public ScriptContextModel(string csxPath, ProjectInfo project, HashSet<string> implicitAssemblyReferences)
        {
            Path = csxPath;
            ImplicitAssemblyReferences = implicitAssemblyReferences;
            ProjectModel = new ProjectInfoModel(project);
            CommonUsings = ScriptProjectSystem.DefaultNamespaces;
            GlobalsType = project.HostObjectType;
        }

        public string Path { get; }

        public HashSet<string> ImplicitAssemblyReferences { get; }

        public Type GlobalsType { get; }

        public ProjectInfoModel ProjectModel { get; }

        public IEnumerable<string> CommonUsings { get; }
    }
}