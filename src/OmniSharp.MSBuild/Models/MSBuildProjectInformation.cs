using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using NuGet.Frameworks;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.Models
{
    public class MSBuildProjectInformation
    {
        public Guid ProjectGuid { get; set; }
        public string Path { get; set; }
        public string AssemblyName { get; set; }
        public string TargetPath { get; set; }
        public string TargetFramework { get; set; }
        public IList<string> SourceFiles { get; set; }
        public IList<TargetFramework> TargetFrameworks { get; set; }
        public string OutputPath { get; set; }
        public bool IsExe { get; set; }

        public MSBuildProjectInformation(ProjectFileInfo projectFileInfo)
        {
            AssemblyName = projectFileInfo.AssemblyName;
            Path = projectFileInfo.ProjectFilePath;
            TargetPath = projectFileInfo.TargetPath;
            ProjectGuid = projectFileInfo.ProjectGuid;
            TargetFramework = projectFileInfo.TargetFramework.ToString();
            SourceFiles = projectFileInfo.SourceFiles;

            var targetFrameworks = new List<TargetFramework>();
            foreach (var targetFramework in projectFileInfo.TargetFrameworks)
            {
                try
                {
                    var framework = NuGetFramework.Parse(targetFramework);
                    targetFrameworks.Add(new TargetFramework(framework));
                }
                catch
                {
                    // If the value can't be parsed, ignore it.
                }
            }

            TargetFrameworks = targetFrameworks;

            OutputPath = projectFileInfo.OutputPath;
            IsExe = projectFileInfo.OutputKind == OutputKind.ConsoleApplication ||
                projectFileInfo.OutputKind == OutputKind.WindowsApplication;
        }
    }
}