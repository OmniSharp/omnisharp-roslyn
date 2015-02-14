using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild
{
    public class MSBuildContext
    {
        public Dictionary<Guid, ProjectId> ProjectGuidToWorkspaceMapping { get; } = new Dictionary<Guid, ProjectId>();
        public Dictionary<string, ProjectFileInfo> Projects { get; } = new Dictionary<string, ProjectFileInfo>();
        public string SolutionPath { get; set; }

        public ProjectFileInfo GetProject(string path)
        {
            ProjectFileInfo p;
            if (!Projects.TryGetValue(path, out p))
            {
                return null;
            }

            return p;
        }
    }
}