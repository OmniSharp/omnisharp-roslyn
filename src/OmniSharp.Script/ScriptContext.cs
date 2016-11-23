using System;
using System.Collections.Generic;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Script
{
    [Export, Shared]
    public class ScriptContext
    {
        public HashSet<string> CsxFilesBeingProcessed { get; } = new HashSet<string>();

        // All of the followings are keyed with the file path
        // Each .csx file is wrapped into a project
        public Dictionary<string, ProjectInfo> CsxFileProjects { get; } = new Dictionary<string, ProjectInfo>();
        public Dictionary<string, List<PortableExecutableReference>> CsxReferences { get; } = new Dictionary<string, List<PortableExecutableReference>>();
        public Dictionary<string, List<ProjectInfo>> CsxLoadReferences { get; } = new Dictionary<string, List<ProjectInfo>>();
        public Dictionary<string, List<string>> CsxUsings { get; } = new Dictionary<string, List<string>>();
        public HashSet<MetadataReference> CommonReferences { get; } = new HashSet<MetadataReference>();
        public HashSet<string> CommonUsings { get; } = new HashSet<string> { "System" };
        public string RootPath { get; set; }
    }
}
