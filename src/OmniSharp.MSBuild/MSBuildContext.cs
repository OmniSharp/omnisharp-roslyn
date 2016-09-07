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
        public Dictionary<Guid, ProjectId> ProjectGuidToProjectIdMap { get; } = new Dictionary<Guid, ProjectId>();
        public Dictionary<string, ProjectFileInfo> Projects { get; } = new Dictionary<string, ProjectFileInfo>(StringComparer.OrdinalIgnoreCase);
        public string SolutionPath { get; set; }
    }
}
