using System;
using System.Collections.Generic;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.Models
{
    public class MSBuildProject
    {
        public Guid ProjectGuid { get; set; }
        public string Path { get; set; }
        public string AssemblyName { get; set; }
        public string TargetPath { get; set; }
        public string TargetFramework { get; set; }
        public IList<string> SourceFiles { get; set; }
        public IList<TargetFramework> TargetFrameworks { get; set; }

        public MSBuildProject(ProjectFileInfo projectFileInfo)
        {
            AssemblyName = projectFileInfo.AssemblyName;
            Path = projectFileInfo.ProjectFilePath;
            TargetPath = projectFileInfo.TargetPath;
            ProjectGuid = projectFileInfo.ProjectGuid;
            TargetFramework = projectFileInfo.TargetFramework.ToString();
            SourceFiles = projectFileInfo.SourceFiles;
        }
    }
}