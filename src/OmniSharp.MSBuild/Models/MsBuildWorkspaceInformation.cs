using System.Collections.Generic;
using System.Linq;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.Models
{
    public class MsBuildWorkspaceInformation
    {
        public MsBuildWorkspaceInformation(string solutionFilePath, IEnumerable<ProjectFileInfo> projects, bool excludeSourceFiles)
        {
            SolutionPath = solutionFilePath;

            Projects = projects
                .OrderBy(x => x.AssemblyName)
                .Select(p => {
                    var project = new MSBuildProjectInformation(p);
                    if (excludeSourceFiles)
                    {
                        project.SourceFiles = null;
                    }

                    return project;
                });
        }

        public string SolutionPath { get; }
        public IEnumerable<MSBuildProjectInformation> Projects { get; }
    }
}