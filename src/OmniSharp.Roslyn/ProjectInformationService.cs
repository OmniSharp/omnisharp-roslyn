using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Models.ProjectInformation;
using OmniSharp.Services;

namespace OmniSharp
{
    [OmniSharpHandler(OmniSharpEndpoints.ProjectInformation, "Projects")]
    public class ProjectInformationService : IRequestHandler<ProjectInformationRequest, ProjectInformationResponse>
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
                var project = await projectSystem.GetProjectModelAsync(request.FileName);
                response.Add($"{projectSystem.Key}Project", project);
            }

            return response;
        }
    }
}
