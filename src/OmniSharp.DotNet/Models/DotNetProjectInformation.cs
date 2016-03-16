using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;

namespace OmniSharp.DotNet.Models
{
    internal class DotNetProjectInformation
    {
        public DotNetProjectInformation(ProjectContext projectContext, string configuration, bool includeSourceFiles = false)
        {
            this.Path = projectContext.RootProject.Path;
            this.Name = projectContext.ProjectFile.Name;

            var outputPaths = projectContext.GetOutputPaths(configuration);
            this.CompilationOutputPath = outputPaths.CompilationOutputPath;
            this.CompilationOutputAssemblyFile = outputPaths.CompilationFiles.Assembly;
            this.CompilationOutputPdbFile = outputPaths.CompilationFiles.PdbPath;

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
        public string CompilationOutputAssemblyFile { get; }
        public string CompilationOutputPdbFile { get; }

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
