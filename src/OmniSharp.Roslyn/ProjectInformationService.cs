using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp
{
    [OmniSharpHandler(OmnisharpEndpoints.ProjectInformation, "Projects")]
    public class ProjectInformationService : RequestHandler<ProjectInformationRequest, ProjectInformationResponse>
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public ProjectInformationService([ImportMany] IEnumerable<IProjectSystem> projectSystems, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProjectInformationService>();
            _projectSystems = projectSystems;
        }

        public async Task<ProjectInformationResponse> Handle(ProjectInformationRequest request)
        {
            _logger.LogInformation("Handing");
            var response = new ProjectInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var project = await projectSystem.GetProjectModel(request.FileName);

                _logger.LogInformation($"with file {request.FileName} in system {projectSystem.Key}");
                response.Add($"{projectSystem.Key}Project", project);
            }

            return response;
        }
    }
}
