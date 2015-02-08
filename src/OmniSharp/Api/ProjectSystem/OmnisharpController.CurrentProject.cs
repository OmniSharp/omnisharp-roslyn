using Microsoft.AspNet.Mvc;
using OmniSharp.AspNet5;
using OmniSharp.Models;
using OmniSharp.MSBuild;
using System.Linq;

namespace OmniSharp
{
    public class CurrentProjectController
    {
        private readonly AspNet5Context _aspnet5Context;
        private readonly OmnisharpWorkspace _workspace;
        private readonly MSBuildContext _msbuildContext;

        public CurrentProjectController(AspNet5Context aspnet5Context, MSBuildContext msbuildContext, OmnisharpWorkspace workspace)
        {
            _aspnet5Context = aspnet5Context;
            _msbuildContext = msbuildContext;
            _workspace = workspace;
        }


        [HttpPost("whereami")]
        public CurrentProjectResponse CurrentProject([FromBody]Request request)
        {
            var response = new CurrentProjectResponse();

            var document = _workspace.GetDocument(request.FileName);
            if (document != null)
            {
                var currentProjectName = document.Project.Name;
                if (currentProjectName.Contains("+"))
                {
                    currentProjectName = currentProjectName.Substring(0, currentProjectName.IndexOf("+"));
                }

                var aspnet5Info = new AspNet5WorkspaceInformation(_aspnet5Context);

                var currentProject = aspnet5Info.Projects.FirstOrDefault(x => x.Name == currentProjectName);
                if (currentProject != null)
                {
                    response.Commands = currentProject.Commands;
                }
            }

            return response;
        }
    }
}