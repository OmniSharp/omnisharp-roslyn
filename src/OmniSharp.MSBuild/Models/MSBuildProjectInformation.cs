using System;
using System.Collections.Generic;
using System.Linq;
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

        public MSBuildProjectInformation(ProjectFileInfo projectFileInfo)
        {
            AssemblyName = projectFileInfo.AssemblyName;
            Path = projectFileInfo.ProjectFilePath;
            TargetPath = projectFileInfo.TargetPath;
            ProjectGuid = projectFileInfo.ProjectGuid;
            TargetFramework = projectFileInfo.TargetFramework.ToString();
            SourceFiles = projectFileInfo.SourceFiles;

            TargetFrameworks = projectFileInfo.TargetFrameworks
                .Select(f => new TargetFramework(f))
                .ToArray();
        }
    }
}