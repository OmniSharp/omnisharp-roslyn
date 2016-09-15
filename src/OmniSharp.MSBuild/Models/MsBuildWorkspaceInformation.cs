using System.Collections.Generic;
using System.Linq;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.Models
{
    public class MsBuildWorkspaceInformation
    {
        public MsBuildWorkspaceInformation(string solutionFilePath, Dictionary<string, ProjectFileInfo> projects, bool excludeSourceFiles)
        {
            SolutionPath = solutionFilePath;

            Projects = projects.Values
                .OrderBy(x => x.AssemblyName)
                .Select(p => {
                    var project = new MSBuildProject(p);
                    if (excludeSourceFiles)
                    {
                        project.SourceFiles = null;
                    }

                    return project;
                });
        }

        public string SolutionPath { get; }
        public IEnumerable<MSBuildProject> Projects { get; }
    }
}