using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.Script
{
    public class ScriptContextModel
    {
        public ScriptContextModel(string csxPath, ProjectInfo project)
        {
            Path = csxPath;
            AssemblyReferences = project.MetadataReferences.Select(a =>
            {
                if (a is PortableExecutableReference portableExecutableReference)
                {
                    return portableExecutableReference.FilePath ?? portableExecutableReference.Display;
                }

                return a.Display;
            });
            CommonUsings = ((CSharpCompilationOptions)(project.CompilationOptions)).Usings;
            GlobalsType = project.HostObjectType;
        }

        public string Path { get; }

        public IEnumerable<string> AssemblyReferences { get; }

        public Type GlobalsType { get; }

        public IEnumerable<string> CommonUsings { get; }
    }
}
