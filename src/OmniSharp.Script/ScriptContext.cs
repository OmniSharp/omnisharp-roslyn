using System;
using System.Collections.Generic;
using Dotnet.Script.DependencyModel.Compilation;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Script
{
    public class ScriptContext
    {
        public ScriptContext(ScriptProjectProvider scriptProjectProvider, HashSet<MetadataReference> metadataReferences, HashSet<string> assemblyReferences, CompilationDependency[] compilationDependencies, Type globalsType)
        {
            ScriptProjectProvider = scriptProjectProvider;
            MetadataReferences = metadataReferences;
            AssemblyReferences = assemblyReferences;
            CompilationDependencies = compilationDependencies;
            GlobalsType = globalsType;
        }

        public ScriptProjectProvider ScriptProjectProvider { get; }

        public HashSet<MetadataReference> MetadataReferences { get; }

        public HashSet<string> AssemblyReferences { get; }

        public CompilationDependency[] CompilationDependencies { get; }

        public Type GlobalsType { get; } 
    }
}
