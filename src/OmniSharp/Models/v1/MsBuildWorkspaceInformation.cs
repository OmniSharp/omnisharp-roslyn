using System.Collections.Generic;
using System.Linq;
using OmniSharp.MSBuild;

namespace OmniSharp.Models
{
    public class MsBuildWorkspaceInformation
    {
        public MsBuildWorkspaceInformation(MSBuildContext msbuildContext, bool excludeSourceFiles)
        {
            SolutionPath = msbuildContext.SolutionPath;

            Projects = msbuildContext
                .Projects
                .Values
                .OrderBy(x => x.AssemblyName)
                .Select(p => {
                    var project = new MSBuildProject(p);
                    if (excludeSourceFiles)
                        project.SourceFiles = null;
                    return project;
                });
        }

        public string SolutionPath { get; }
        public IEnumerable<MSBuildProject> Projects { get; }
    }
}