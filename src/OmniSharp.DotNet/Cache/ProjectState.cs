using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ProjectModel;

namespace OmniSharp.DotNet.Cache
{
    public class ProjectState
    {
        public ProjectState(ProjectId id, ProjectContext context)
        {
            Id = id;
            ProjectContext = context;
        }

        public ProjectId Id { get; }

        public ProjectContext ProjectContext { get; set; }

        public Dictionary<string, MetadataReference> FileMetadataReferences { get; } = new Dictionary<string, MetadataReference>();

        public Dictionary<string, ProjectId> ProjectReferences { get; } = new Dictionary<string, ProjectId>();

        public Dictionary<string, DocumentId> DocumentReferences { get; } = new Dictionary<string, DocumentId>();

        public override string ToString()
        {
            return $"[{nameof(ProjectState)}] {ProjectContext.ProjectFile.Name}/{ProjectContext.TargetFramework}";
        }
    }
}
