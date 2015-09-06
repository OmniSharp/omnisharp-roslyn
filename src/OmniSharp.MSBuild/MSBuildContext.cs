using System;
using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild
{
    [Export, Shared]
    public class MSBuildContext
    {
        public Dictionary<Guid, ProjectId> ProjectGuidToWorkspaceMapping { get; } = new Dictionary<Guid, ProjectId>();
        public Dictionary<string, ProjectFileInfo> Projects { get; } = new Dictionary<string, ProjectFileInfo>();
        public string SolutionPath { get; set; }
    }
}
