using System;
using System.Linq;
using System.Collections.Generic;
using OmniSharp.AspNet5;
using OmniSharp.MSBuild;

namespace OmniSharp.Models
{
    public class WorkspaceInformationResponse
    {
        public AspNet5WorkspaceInformation AspNet5 { get; set; }
        public MsBuildWorkspaceInformation MSBuild { get; set; }
    }

    public class MsBuildWorkspaceInformation
    {
        public MsBuildWorkspaceInformation(MSBuildContext msbuildContext)
        {
            SolutionPath = msbuildContext.SolutionPath;

            Projects = msbuildContext.Projects.Values.Select(p => new MSBuildProject
            {
                AssemblyName = p.AssemblyName,
                Path = p.ProjectFilePath,
                TargetPath = p.TargetPath,
                ProjectGuid = p.ProjectId,
                TargetFramework = p.TargetFramework.ToString()
            });
        }

        public string SolutionPath { get; }
        public IEnumerable<MSBuildProject> Projects { get; }
    }

    public class AspNet5WorkspaceInformation
    {
        private AspNet5Context _context;

        public AspNet5WorkspaceInformation(AspNet5Context context)
        {
            _context = context;

            Projects = context.Projects.Values.Select(project => new AspNet5Project
            {
                Path = project.Path,
                Name = project.Name,
                Commands = project.Commands,
                Configurations = project.Configurations,
                GlobalJsonPath = project.GlobalJsonPath,
                ProjectSearchPaths = project.ProjectSearchPaths,
                Frameworks = project.ProjectsByFramework.Keys.ToList()
            });

            RuntimePath = context.RuntimePath;
            DesignTimeHostPort = context.DesignTimeHostPort; 
        }

        public IEnumerable<AspNet5Project> Projects { get; }
        public string RuntimePath { get; }
        public int DesignTimeHostPort { get; }
    }

    public class MSBuildProject
    {
        public Guid ProjectGuid { get; set; }
        public string Path { get; set; }
        public string AssemblyName { get; set; }
        public string TargetPath { get; set; }
        public string TargetFramework { get; set; }
    }

    public class AspNet5Project
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public IDictionary<string, string> Commands { get; set; }
        public IList<string> Configurations { get; set; }
        public IList<string> ProjectSearchPaths { get; set; }
        public IList<string> Frameworks { get; set; }
        public string GlobalJsonPath { get; set; }
    }
}