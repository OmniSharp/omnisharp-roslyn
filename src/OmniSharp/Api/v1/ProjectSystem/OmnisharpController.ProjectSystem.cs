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
            var tasks = new List<Task<Action>>();
            var response = new WorkspaceInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                tasks.Add(projectSystem.GetInformationModel(request)
                    .ContinueWith((information) =>
                    {
                        Action action = () =>
                        {
                            response.Add(projectSystem.Key, information);
                        };

                        return action;
                    }));
            }

            var results = await Task.WhenAll(tasks);
            foreach (var result in results)
            {
                result();
            }
            return response;
        }

        [HttpPost("/project")]
        public async Task<ProjectInformationResponse> CurrentProject(Request request)
        {
            var tasks = new List<Task<Action>>();
            var response = new ProjectInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                tasks.Add(projectSystem.GetProjectModel(request.FileName)
                    .ContinueWith((project) =>
                    {
                        Action action = () =>
                        {
                            if (project != null)
                                response.Add(projectSystem.Key, project);
                        };

                        return action;
                    }));
            }

            var results = await Task.WhenAll(tasks);
            foreach (var result in results)
            {
                result();
            }

            return response;
        }
    }
}
