using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp
{
    [OmniSharpHandler(OmniSharpEndpoints.WorkspaceInformation, "Projects")]
    public class WorkspaceInformationService : IRequestHandler<WorkspaceInformationRequest, WorkspaceInformationResponse>
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
                var workspaceModel = await projectSystem.GetWorkspaceModelAsync(request);
                response.Add(projectSystem.Key, workspaceModel);
            }

            return response;
        }
    }
}
