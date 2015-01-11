using Microsoft.AspNet.Mvc;
using OmniSharp.AspNet5;
using OmniSharp.Models;

namespace OmniSharp
{
    public class ProjectSystemController
    {
        private readonly AspNet5Context _aspnet5Context;

        public ProjectSystemController(AspNet5Context aspnet5Context)
        {
            _aspnet5Context = aspnet5Context;
        }

        [HttpGet("/projects")]
        public WorkspaceInformationResponse ProjectInformation()
        {
            return new WorkspaceInformationResponse
            {
                 AspNet5 = new AspNet5WorkspaceInformation(_aspnet5Context)
            };
        }
    }
}