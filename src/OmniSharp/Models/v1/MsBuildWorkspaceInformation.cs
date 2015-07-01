using System.Collections.Generic;
using System.Linq;
using OmniSharp.MSBuild;

namespace OmniSharp.Models
{
    public class MsBuildWorkspaceInformation
    {
        public MsBuildWorkspaceInformation(MSBuildContext msbuildContext, bool includeSourceFiles)
        {
            SolutionPath = msbuildContext.SolutionPath;

            Projects = msbuildContext
                .Projects
                .Values
                .OrderBy(x => x.AssemblyName)
                .Select(p => {
                    var prj = new MSBuildProject(p);
                    if (!includeSourceFiles)
                        prj.SourceFiles = null;
                    return prj;
                });
        }

        public string SolutionPath { get; }
        public IEnumerable<MSBuildProject> Projects { get; }
    }
}