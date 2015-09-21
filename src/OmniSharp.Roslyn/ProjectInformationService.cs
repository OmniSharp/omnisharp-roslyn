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
    [OmniSharpHandler(typeof(RequestHandler<ProjectInformationRequest, ProjectInformationResponse>), "Projects")]
    public class ProjectInformationService : RequestHandler<ProjectInformationRequest, ProjectInformationResponse>
    {
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public ProjectInformationService([ImportMany] IEnumerable<IProjectSystem> projectSystems)
        {
            _projectSystems = projectSystems;
        }

        public async Task<ProjectInformationResponse> Handle(ProjectInformationRequest request)
        {
            var response = new ProjectInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var project = await projectSystem.GetProjectModel(request.FileName);
                response.Add(projectSystem.Key, project);
            }

            return response;
        }
    }
}
