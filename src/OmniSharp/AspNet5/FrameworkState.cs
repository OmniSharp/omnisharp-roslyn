using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.AspNet5
{
    public class FrameworkState
    {
        public ProjectId ProjectId { get; set; }

        public Dictionary<string, DocumentId> Documents { get; set; }

        public Dictionary<string, MetadataReference> FileReferences { get; set; }

        public Dictionary<string, MetadataReference> RawReferences { get; set; }

        public Dictionary<string, ProjectId> ProjectReferences { get; set; }

        public HashSet<string> ProjectDependeees { get; set; }

        public List<ProjectId> PendingProjectReferences { get; set; }

        public ProjectState ProjectState { get; private set; }

        public bool Loaded { get; set; }

        public FrameworkState(ProjectState projectState)
        {
            ProjectState = projectState;
            ProjectId = ProjectId.CreateNewId();
            Documents = new Dictionary<string, DocumentId>();
            FileReferences = new Dictionary<string, MetadataReference>();
            RawReferences = new Dictionary<string, MetadataReference>();
            ProjectReferences = new Dictionary<string, ProjectId>();
            PendingProjectReferences = new List<ProjectId>();
            ProjectDependeees = new HashSet<string>();
        }
    }
}