using Microsoft.AspNet.Mvc;
using OmniSharp.AspNet5;
using OmniSharp.Models;
using OmniSharp.MSBuild;
using OmniSharp.Services;
using System;
using System.Collections.Generic;

namespace OmniSharp
{
    public class ProjectSystemController
    {
        private readonly AspNet5Context _aspnet5Context;
        private readonly OmnisharpWorkspace _workspace;
        private readonly MSBuildContext _msbuildContext;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        public ProjectSystemController(AspNet5Context aspnet5Context, MSBuildContext msbuildContext, OmnisharpWorkspace workspace, IEnumerable<IProjectSystem> projectSystems)
        {
            _aspnet5Context = aspnet5Context;
            _msbuildContext = msbuildContext;
            _workspace = workspace;
            _projectSystems = projectSystems;
        }

        [HttpPost("/projects")]
        [HttpGet("/projects")]
        public WorkspaceInformationResponse ProjectInformation()
        {
            return new WorkspaceInformationResponse
            {
                MSBuild = new MsBuildWorkspaceInformation(_msbuildContext),
                AspNet5 = new AspNet5WorkspaceInformation(_aspnet5Context)
            };
        }

        [HttpPost("/project")]
        public ProjectInformationResponse CurrentProject(Request request)
        {
            var document = _workspace.GetDocument(request.FileName);

            var msBuildContextProject = _msbuildContext?.GetProject(document?.Project.FilePath);
            var aspNet5ContextProject = _aspnet5Context?.GetProject(document?.Project.FilePath);

            MSBuildProject msBuildProjectItem = null;
            AspNet5Project aspNet5ProjectItem = null;

            if (msBuildContextProject != null)
            {
                msBuildProjectItem = new MSBuildProject(msBuildContextProject);
            }
            if (aspNet5ContextProject != null)
            {
                aspNet5ProjectItem = new AspNet5Project(aspNet5ContextProject);
            }

            return new ProjectInformationResponse
            {
                MsBuildProject = msBuildProjectItem,
                AspNet5Project = aspNet5ProjectItem
            };
        }


        [HttpGet("/reloadsolution")]
        public bool ReloadSolution()
        {
            try
            {
                foreach (var projectSystem in _projectSystems)
                {
                    projectSystem.Initalize();
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }
    }
}