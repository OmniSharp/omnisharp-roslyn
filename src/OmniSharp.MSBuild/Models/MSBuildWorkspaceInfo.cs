using System.Collections.Generic;
using System.Linq;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild.Models
{
    public class MSBuildWorkspaceInfo
    {
        public MSBuildWorkspaceInfo(string solutionFilePath, IEnumerable<ProjectFileInfo> projects, bool excludeSourceFiles)
        {
            SolutionPath = solutionFilePath;

            Projects = projects
                .OrderBy(x => x.AssemblyName)
                .Select(p => {
                    var project = new MSBuildProjectInfo(p);
                    if (excludeSourceFiles)
                    {
                        project.SourceFiles = null;
                    }

                    return project;
                });
        }

        public string SolutionPath { get; }
        public IEnumerable<MSBuildProjectInfo> Projects { get; }
    }
}