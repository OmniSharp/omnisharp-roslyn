using Microsoft.AspNet.Mvc;
using OmniSharp.AspNet5;
using OmniSharp.Models;
using OmniSharp.MSBuild;

namespace OmniSharp
{
    public class ProjectSystemController
    {
        private readonly AspNet5Context _aspnet5Context;
        private readonly MSBuildContext _msbuildContext;

        public ProjectSystemController(AspNet5Context aspnet5Context, MSBuildContext msbuildContext)
        {
            _aspnet5Context = aspnet5Context;
            _msbuildContext = msbuildContext;
        }

        [HttpGet("/projects")]
        public WorkspaceInformationResponse ProjectInformation()
        {
            return new WorkspaceInformationResponse
            {
                MSBuild = new MsBuildWorkspaceInformation(_msbuildContext),
                AspNet5 = new AspNet5WorkspaceInformation(_aspnet5Context)
            };
        }
    }
}