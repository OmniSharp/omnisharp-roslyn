using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Composition;

namespace OmniSharp.ScriptCs
{
    [Export, Shared]
    public class ScriptCsContext
    {
        public HashSet<string> CsxFilesBeingProcessed { get; } = new HashSet<string>();

        // All of the followings are keyed with the file path
        // Each .csx file is wrapped into a project
        public Dictionary<string, ProjectInfo> CsxFileProjects { get; } = new Dictionary<string, ProjectInfo>();
        public Dictionary<string, List<MetadataReference>> CsxReferences { get; } = new Dictionary<string, List<MetadataReference>>();
        public Dictionary<string, List<ProjectInfo>> CsxLoadReferences { get; } = new Dictionary<string, List<ProjectInfo>>();
        public Dictionary<string, List<string>> CsxUsings { get; } = new Dictionary<string, List<string>>();

        public HashSet<string> ScriptPacks { get; } = new HashSet<string>();

        // Nuget and ScriptPack stuff
        public HashSet<MetadataReference> CommonReferences { get; } = new HashSet<MetadataReference>();
        public HashSet<string> CommonUsings { get; } = new HashSet<string>();

        public string RootPath { get; set; }
    }
}
