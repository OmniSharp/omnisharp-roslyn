using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp
{
    [OmniSharpHandler(typeof(RequestHandler<WorkspaceInformationRequest, WorkspaceInformationResponse>), "Projects")]
    public class WorkspaceInformationService : RequestHandler<WorkspaceInformationRequest, WorkspaceInformationResponse>
    {
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public WorkspaceInformationService([ImportMany] IEnumerable<IProjectSystem> projectSystems)
        {
            _projectSystems = projectSystems;
        }

        public async Task<WorkspaceInformationResponse> Handle(WorkspaceInformationRequest request)
        {
            var response = new WorkspaceInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var project = await projectSystem.GetInformationModel(request);
                response.Add(projectSystem.Key, project);
            }

            return response;
        }
    }
}
