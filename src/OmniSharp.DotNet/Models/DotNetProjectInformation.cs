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
            this.TargetFramework = new DotNetFramework(projectContext.TargetFramework);

            var outputPaths = projectContext.GetOutputPaths(configuration);
            this.CompilationOutputPath = outputPaths.CompilationOutputPath;
            this.CompilationOutputAssemblyFile = outputPaths.CompilationFiles.Assembly;
            this.CompilationOutputPdbFile = outputPaths.CompilationFiles.PdbPath;

            var compilationOptions = projectContext.ProjectFile.GetCompilerOptions(targetFramework: projectContext.TargetFramework, configurationName: configuration);
            this.EmitEntryPoint = compilationOptions.EmitEntryPoint;

            var sourceFiles = new List<string>();

            if (includeSourceFiles)
            {
                sourceFiles.AddRange(projectContext.ProjectFile.Files.SourceFiles);
            }

            this.SourceFiles = sourceFiles;
        } 

        public string Path { get; }
        public string Name { get; }
        public DotNetFramework TargetFramework { get; }

        public string CompilationOutputPath { get; }
        public string CompilationOutputAssemblyFile { get; }
        public string CompilationOutputPdbFile { get; }
        public bool? EmitEntryPoint { get; }

        public IReadOnlyList<string> SourceFiles { get; }

    }
}
