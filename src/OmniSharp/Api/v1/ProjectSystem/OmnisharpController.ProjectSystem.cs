using Microsoft.AspNet.Mvc;
using OmniSharp.Dnx;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.MSBuild;
using OmniSharp.ScriptCs;
using OmniSharp.Extensions;

#if DNX451
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
#endif

namespace OmniSharp
{
    public class ProjectSystemController
    {
        private readonly DnxContext _dnxContext;
        private readonly OmnisharpWorkspace _workspace;
        private readonly MSBuildContext _msbuildContext;
        private readonly ScriptCsContext _scriptCsContext;

        public ProjectSystemController(DnxContext dnxContext, MSBuildContext msbuildContext, ScriptCsContext scriptCsContext,
            OmnisharpWorkspace workspace)
        {
            _dnxContext = dnxContext;
            _msbuildContext = msbuildContext;
            _scriptCsContext = scriptCsContext;
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
                MsBuildProject = msBuildProjectItem,
                DnxProject = dnxProjectItem
            };
        }

#if DNX451
        [HttpPost("/addtoproject")]
        public void AddToProject(Request request)
        {
            if (request.FileName == null || !File.Exists(request.FileName))
            {
                return;
            }
            
            var project = GetRelativeMsBuildProject(request.FileName);

            if (project == null)
            {
                return;
            }

            var itemType = GetItemType(request.FileName);
            var itemAlreadyExists = project.AllEvaluatedItems.Any(i => i.EvaluatedInclude == request.FileName && string.Equals(i.ItemType, itemType, StringComparison.OrdinalIgnoreCase));
            if (itemAlreadyExists)
            {
                return;
            }

            project.AddItem(itemType, GetRelativeFileName(request.FileName, project.FullPath));
            project.Save();
        }

        [HttpPost("/removefromproject")]
        public void RemoveFromProject(Request request)
        {
            if (request.FileName == null || !File.Exists(request.FileName))
            {
                return;
            }

            var project = GetRelativeMsBuildProject(request.FileName);

            if (project == null)
            {
                return;
            }

            var itemType = GetItemType(request.FileName);

            var item = project.AllEvaluatedItems
                .Where(i => string.Equals(i.ItemType, itemType, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(i => i.EvaluatedInclude == GetRelativeFileName(request.FileName, project.FullPath) || i.EvaluatedInclude == request.FileName);

            if (item == null)
            {
                return;
            }

            project.RemoveItem(item);
            project.Save();
        }

        private static string GetItemType(string fileName)
        {
            return new FileInfo(fileName).Extension == ".cs" ? "Compile" : "Content";
        }

        private static string GetRelativeFileName(string filename, string projectFile)
        {
            return new Uri(projectFile)
                .MakeRelativeUri(new Uri(filename))
                .ToString()
                .ForceWindowsPathSeparator();
        }


        public Microsoft.Build.Evaluation.Project GetRelativeMsBuildProject(string filename)
        {
            var relativeProject = FindRelativeProject(filename);

            if (relativeProject == null || new FileInfo(relativeProject.FilePath).Extension != ".csproj")
            {
                return null;
            }

            Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            return new Microsoft.Build.Evaluation.Project(relativeProject.FilePath);
        }

        public Microsoft.CodeAnalysis.Project FindRelativeProject(string filename)
        {
            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                var directory = Path.GetDirectoryName(project.FilePath);

                if (filename.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }
#endif
    }
}