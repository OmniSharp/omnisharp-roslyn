using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Dnx;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp
{
    public class ProjectSystemController
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        public ProjectSystemController(OmnisharpWorkspace workspace, CompositionHost host)
        {
            _projectSystems = host.GetExports<IProjectSystem>();
            _workspace = workspace;
        }

        [HttpPost("/projects")]
        [HttpGet("/projects")]
        public async Task<WorkspaceInformationResponse> ProjectInformation(ProjectInformationRequest request)
        {
            var response = new WorkspaceInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var information = await projectSystem.GetInformationModel(request);
                response.Add(projectSystem.Key, information);
            }

            return response;
        }

        [HttpPost("/project")]
        public async Task<ProjectInformationResponse> CurrentProject(Request request)
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
