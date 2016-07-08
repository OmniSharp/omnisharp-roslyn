using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Workspaces;

namespace OmniSharp.Roslyn.CSharp.Tests.Utility
{
    public class WorkspaceHelper
    {
        public static IEnumerable<Microsoft.CodeAnalysis.Workspace> Create(string projectPath)
        {
            var builder = new ProjectContextBuilder().WithProjectDirectory(projectPath);
            
            return builder.BuildAllTargets().Select(context => context.CreateRoslynWorkspace());
        }
    }
}