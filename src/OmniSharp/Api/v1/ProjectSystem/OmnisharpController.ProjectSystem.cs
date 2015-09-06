using System.Collections.Generic;
﻿using Microsoft.AspNet.Mvc;
using OmniSharp.Dnx;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.MSBuild;
using OmniSharp.ScriptCs;
using OmniSharp.Services;

namespace OmniSharp
{
    public class ProjectSystemController
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        public ProjectSystemController(IEnumerable<IProjectSystem> projectSystems,            OmnisharpWorkspace workspace)
        {
            _projectSystems = projectSystems;
            _workspace = workspace;
        }

        [HttpPost("/projects")]
        [HttpGet("/projects")]
        public WorkspaceInformationResponse ProjectInformation(ProjectInformationRequest request)
        {
            return new WorkspaceInformationResponse
            {
                MSBuild = new MsBuildWorkspaceInformation(_msbuildContext, request?.ExcludeSourceFiles ?? false),
                Dnx = new DnxWorkspaceInformation(_dnxContext),
                ScriptCs = _scriptCsContext
            };
        }

        [HttpPost("/project")]
        public ProjectInformationResponse CurrentProject(Request request)
        {
            var document = _workspace.GetDocument(request.FileName);

            var msBuildContextProject = _msbuildContext?.GetProject(document?.Project.FilePath);
            var dnxContextProject = _dnxContext?.GetProject(document?.Project.FilePath);

            MSBuildProject msBuildProjectItem = null;
            DnxProject dnxProjectItem = null;

            if (msBuildContextProject != null)
            {
                msBuildProjectItem = new MSBuildProject(msBuildContextProject);
            }

            if (dnxContextProject != null)
            {
                dnxProjectItem = new DnxProject(dnxContextProject);
            }

            return new ProjectInformationResponse
            {
                {nameof(MSBuildProject), msBuildProjectItem },
                {nameof(DnxProject), dnxProjectItem}
            };
        }
    }
}
