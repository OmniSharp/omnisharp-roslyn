using Microsoft.DotNet.ProjectModel;
using System.Collections.Generic;

namespace OmniSharp.DotNet.Models
{
    internal class DotNetProjectInformation
    {
        public DotNetProjectInformation(ProjectContext projectContext, string configuration, bool includeSourceFiles = false)
        {
            this.Path = projectContext.RootProject.Path;
            this.Name = projectContext.ProjectFile.Name;
            this.CompilationOutputPath = projectContext.GetOutputPaths(configuration).CompilationOutputPath;

            var sourceFiles = new List<string>();

            if (includeSourceFiles)
            {
                sourceFiles.AddRange(projectContext.ProjectFile.Files.SourceFiles);
            }

            this.SourceFiles = sourceFiles;
        } 

        public string Path { get; }
        public string Name { get; }
        public string CompilationOutputPath { get; }
        public IReadOnlyList<string> SourceFiles { get; }

        //public DotNetProjectInformation(string projectPath, ProjectInformation info)
        //{
        //    Path = projectPath;
        //    Name = info.Name;
        //    Commands = info.Commands;
        //    Configurations = info.Configurations;
        //    ProjectSearchPaths = info.ProjectSearchPaths;
        //    Frameworks = info.Frameworks.Select(framework => new DotNetFramework(framework));
        //    GlobalJsonPath = info.GlobalJsonPath;
        //}

        //public IDictionary<string, string> Commands { get; }
        //public IEnumerable<string> Configurations { get; }
        //public IEnumerable<string> ProjectSearchPaths { get; }
        //public IEnumerable<DotNetFramework> Frameworks { get; }
        //public string GlobalJsonPath { get; }
    }
}
