using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.ProjectModel.ProjectSystem;
using NuGet.Frameworks;

namespace OmniSharp.DotNet.Models
{
    public class ProjectWithFramework
    {
        public ProjectWithFramework(ProjectId id,
                                    string projectPath,
                                    NuGetFramework framework,
                                    ProjectInformation information) 
        {
            Id = id;
            Path = projectPath;
            TargetFramework = framework;
            Information = information;
        }

        public ProjectId Id { get; }

        public ProjectInformation Information { get; }

        public string Path { get; }

        public NuGetFramework TargetFramework { get; }

        public Dictionary<string, MetadataReference> FileMetadataReferences { get; } = new Dictionary<string, MetadataReference>();

        public Dictionary<string, ProjectId> ProjectReferences { get; } = new Dictionary<string, ProjectId>();

        public override bool Equals(object obj)
        {
            var other = obj as ProjectWithFramework;
            return other != null &&
                   other.Id == Id;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
