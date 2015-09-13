using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Dnx;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp
{
    public class ProjectSystemMiddleware
    {
        private readonly IEnumerable<IProjectSystem> _projectSystems;
        private readonly RequestDelegate _next;

        public ProjectSystemMiddleware(RequestDelegate next, CompositionHost host)
        {
            _projectSystems = host.GetExports<IProjectSystem>();
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (endpoint == "/projects")
                {
                    var request = DeserializeRequestObject(httpContext.Request.Body)
                        .ToObject<ProjectInformationRequest>();
                    var response = await GetWorkspaceInformation(request);
                    SerializeResponseObject(httpContext.Response, response);
                    return;
                }

                if (endpoint == "/project") {
                    var request = DeserializeRequestObject(httpContext.Request.Body)
                        .ToObject<Request>();
                    var response = await GetProjectInformation(request);
                    SerializeResponseObject(httpContext.Response, response);
                    return;
                }
            }

            await _next(httpContext);
        }

        private JObject DeserializeRequestObject(Stream readStream)
        {
            return JObject.Load(new JsonTextReader(new StreamReader(readStream)));
        }

        private void SerializeResponseObject(HttpResponse response, object value)
        {
            using (var writer = new StreamWriter(response.Body))
            {
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    jsonWriter.CloseOutput = false;
                    var jsonSerializer = JsonSerializer.Create(/*TODO: SerializerSettings*/);
                    jsonSerializer.Serialize(jsonWriter, value);
                }
            }
        }

        private async Task<WorkspaceInformationResponse> GetWorkspaceInformation(ProjectInformationRequest request)
        {
            var response = new WorkspaceInformationResponse();

            foreach (var projectSystem in _projectSystems)
            {
                var information = await projectSystem.GetInformationModel(request);
                response.Add(projectSystem.Key, information);
            }

            return response;
        }

        private async Task<ProjectInformationResponse> GetProjectInformation(Request request)
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
