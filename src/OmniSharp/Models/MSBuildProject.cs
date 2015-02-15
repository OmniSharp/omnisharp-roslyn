﻿using OmniSharp.MSBuild.ProjectFile;
using System;

namespace OmniSharp.Models
{
    public class MSBuildProject
    {
        public Guid ProjectGuid { get; set; }
        public string Path { get; set; }
        public string AssemblyName { get; set; }
        public string TargetPath { get; set; }
        public string TargetFramework { get; set; }

        public MSBuildProject(ProjectFileInfo p)
        {
            AssemblyName = p.AssemblyName;
            Path = p.ProjectFilePath;
            TargetPath = p.TargetPath;
            ProjectGuid = p.ProjectId;
            TargetFramework = p.TargetFramework.ToString();
        }
    }
}