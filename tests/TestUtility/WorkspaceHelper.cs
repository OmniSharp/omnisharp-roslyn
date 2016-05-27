using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Workspaces;

namespace TestUtility
{
    public class WorkspaceHelper
    {
        /// <summary>
        /// Create Roslyn worksapces from a given project path
        /// </summary>
        public static IEnumerable<Microsoft.CodeAnalysis.Workspace> Create(string projectPath)
        {
            var builder = new ProjectContextBuilder().WithProjectDirectory(projectPath);

            return builder.BuildAllTargets().Select(context => context.CreateRoslynWorkspace());
        }
    }
}