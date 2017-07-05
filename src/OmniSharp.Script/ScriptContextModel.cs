using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Script
{
    public class ScriptContextModel
    {
        public ScriptContextModel(string csxPath, ProjectInfo project, HashSet<string> implicitAssemblyReferences)
        {
            Path = csxPath;
            ImplicitAssemblyReferences = implicitAssemblyReferences;
            CommonUsings = ScriptHelper.DefaultNamespaces;
            GlobalsType = project.HostObjectType;
        }

        public string Path { get; }

        public HashSet<string> ImplicitAssemblyReferences { get; }

        public Type GlobalsType { get; }

        public IEnumerable<string> CommonUsings { get; }
    }
}