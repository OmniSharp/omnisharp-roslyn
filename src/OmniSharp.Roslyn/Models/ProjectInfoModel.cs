using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OmniSharp.Roslyn.Models
{
    public class ReferenceModel
    {
        public ReferenceModel(AnalyzerReference reference)
        {
            Id = reference.Id.ToString();
            Display = reference.Display;
            FullPath = reference.FullPath;
        }

        public ReferenceModel(MetadataReference reference)
        {
            Display = reference.Display;
            Aliases = reference.Properties.Aliases;
            Kind = reference.Properties.Kind.ToString();
        }

        public ReferenceModel(ProjectReference reference)
        {
            Id = reference.ProjectId.Id.ToString();
            Aliases = reference.Aliases;
        }

        public string Id { get; set; }
        public string Display { get; set; }
        public string FullPath { get; set; }
        public IEnumerable<string> Aliases { get; set; }
        public string Kind { get; set; }
    }

    public class ProjectInfoModel
    {
        public ProjectInfoModel(ProjectInfo project)
        {
            FilePath = project.FilePath;
            AssemblyName = project.AssemblyName;
            Name = project.Name;
            Language = project.Language;
            AnalyzerReferences = project.AnalyzerReferences.Select(x => new ReferenceModel(x));
            MetadataReferences = project.MetadataReferences.Select(x => new ReferenceModel(x));
            ProjectReferences = project.ProjectReferences.Select(x => new ReferenceModel(x));
        }

        public string FilePath { get; set; }
        public string AssemblyName { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public IEnumerable<ReferenceModel> AnalyzerReferences { get; set; }
        public IEnumerable<ReferenceModel> MetadataReferences { get; set; }
        public IEnumerable<ReferenceModel> ProjectReferences { get; set; }
    }
}