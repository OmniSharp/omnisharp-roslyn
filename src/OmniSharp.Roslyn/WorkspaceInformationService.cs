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
    [OmniSharpHandler(OmnisharpEndpoints.WorkspaceInformation, "Projects")]
    public class WorkspaceInformationService : RequestHandler<WorkspaceInformationRequest, WorkspaceInformationResponse>
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public WorkspaceInformationService([ImportMany] IEnumerable<IProjectSystem> projectSystems, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<WorkspaceInformationService>();
            _projectSystems = projectSystems;
        }

        public async Task<WorkspaceInformationResponse> Handle(WorkspaceInformationRequest request)
        {
            _logger.LogInformation("Responding");

            var response = new WorkspaceInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var project = await projectSystem.GetInformationModel(request);

                _logger.LogInformation($"with project system {projectSystem.Key}");
                response.Add(projectSystem.Key, project);
            }

            return response;
        }
    }
}
