using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp
{
    [OmniSharpHandler(OmnisharpEndpoints.WorkspaceInformation, "Projects")]
    public class WorkspaceInformationService : RequestHandler<WorkspaceInformationRequest, WorkspaceInformationResponse>
    {
        private readonly IEnumerable<IProjectSystem> _projectSystems;
        
        private readonly ILogger _logger;

        [ImportingConstructor]
        public WorkspaceInformationService([ImportMany] IEnumerable<IProjectSystem> projectSystems, ILoggerFactory loggerFactory)
        {
            _projectSystems = projectSystems;
            _logger = loggerFactory.CreateLogger<WorkspaceInformationService>();
        }

        public async Task<WorkspaceInformationResponse> Handle(WorkspaceInformationRequest request)
        {
            var response = new WorkspaceInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var project = await projectSystem.GetInformationModel(request);
                if (!response.ContainsKey(projectSystem.Key))
                {
                    response.Add(projectSystem.Key, project);
                }
                else
                {
                    _logger.LogWarning("Already had a response for " + projectSystem.Key);
					response[projectSystem.Key] = project;
                }
            }

            return response;
        }
    }
}
