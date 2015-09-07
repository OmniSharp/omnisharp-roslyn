using System.Composition.Hosting;
using System.Collections.Generic;
﻿using Microsoft.AspNet.Mvc;
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
        public WorkspaceInformationResponse ProjectInformation(ProjectInformationRequest request)
        {
            var response = new WorkspaceInformationResponse();
            foreach (var projectSystem in _projectSystems)
            {
                var information = projectSystem.GetInformationModel(request);
                response.Add(projectSystem.Key, information);
            }
            return response;
        }

        [HttpPost("/project")]
        public ProjectInformationResponse CurrentProject(Request request)
        {
            var response = new ProjectInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var project = projectSystem.GetProjectModel(request.FileName);
                if (project != null)
                    response.Add(projectSystem.Key, project);
            }

            return response;
        }
    }
}
