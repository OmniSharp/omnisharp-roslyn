using System;
using System.Collections.Generic;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Script
{
    public class ScriptContext
    {
        public ScriptContext(string entryFilePath)
        {
            EntryFilePath = entryFilePath;
        }

        public string EntryFilePath { get; }
        public HashSet<string> CsxFilesBeingProcessed { get; } = new HashSet<string>();
        public Dictionary<string, HashSet<MetadataReference>> CsxReferences { get; } = new Dictionary<string, HashSet<MetadataReference>>();
        public Dictionary<string, List<string>> CsxLoadReferences { get; } = new Dictionary<string, List<string>>();
        public Dictionary<string, List<string>> CsxUsings { get; } = new Dictionary<string, List<string>>();
        public HashSet<MetadataReference> CommonReferences { get; } = new HashSet<MetadataReference>();
        public HashSet<string> CsxFiles { get; } = new HashSet<string>();
    }
}
