using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Script
{
    public class ScriptContext
    {
        public ScriptContext(ScriptProjectProvider scriptProjectProvider, HashSet<MetadataReference> metadataReferences, CompilationDependency[] compilationDependencies, Type globalsType)
        {
            ScriptProjectProvider = scriptProjectProvider;
            MetadataReferences = metadataReferences;
            CompilationDependencies = compilationDependencies;
            GlobalsType = globalsType;
        }

        public ScriptProjectProvider ScriptProjectProvider { get; }

        public HashSet<MetadataReference> MetadataReferences { get; }

        public CompilationDependency[] CompilationDependencies { get; }

        public Type GlobalsType { get; }
    }
}
