using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;

namespace OmniSharp.Dnx
{
    public class FrameworkProject
    {
        public ProjectId ProjectId { get; set; }

        public string Framework { get; private set; }

        public string FriendlyName { get; private set; }

        public string ShortName { get; private set; }

        public Dictionary<string, DocumentId> Documents { get; set; }

        public Dictionary<string, MetadataReference> FileReferences { get; set; }

        public Dictionary<string, MetadataReference> RawReferences { get; set; }

        public Dictionary<string, ProjectId> ProjectReferences { get; set; }

        public Dictionary<string, ProjectId> ProjectDependeees { get; set; }

        public List<ProjectId> PendingProjectReferences { get; set; }

        public Project Project { get; private set; }

        public bool Loaded { get; set; }

        public FrameworkProject(Project project, FrameworkData frameworkData)
        {
            Project = project;
            Framework = frameworkData.FrameworkName;
            FriendlyName = frameworkData.FriendlyName;
            ShortName = frameworkData.ShortName;
            ProjectId = ProjectId.CreateNewId();
            Documents = new Dictionary<string, DocumentId>();
            FileReferences = new Dictionary<string, MetadataReference>();
            RawReferences = new Dictionary<string, MetadataReference>();
            ProjectReferences = new Dictionary<string, ProjectId>(StringComparer.OrdinalIgnoreCase);
            ProjectDependeees = new Dictionary<string, ProjectId>(StringComparer.OrdinalIgnoreCase);
            PendingProjectReferences = new List<ProjectId>();
        }

        public override string ToString()
        {
            return Project.Name + "+" + Framework + " (" + Project.Path + ")";
        }
    }
}
