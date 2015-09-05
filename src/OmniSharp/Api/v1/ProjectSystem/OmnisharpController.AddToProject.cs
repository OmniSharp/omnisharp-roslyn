using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.Mvc;
using OmniSharp.Dnx;
using OmniSharp.Models;
using OmniSharp.MSBuild;
using OmniSharp.ScriptCs;
using System.Linq;
using System.Xml.Linq;
using OmniSharp.Extensions;

namespace OmniSharp.Api.v1.ProjectSystem
{
    public class OmnisharpController
    {
        private readonly XNamespace _msBuildNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003";

        private readonly DnxContext _dnxContext;
        private readonly OmnisharpWorkspace _workspace;
        private readonly MSBuildContext _msbuildContext;
        private readonly ScriptCsContext _scriptCsContext;

        public OmnisharpController(DnxContext dnxContext, MSBuildContext msbuildContext, ScriptCsContext scriptCsContext, OmnisharpWorkspace workspace)
        {
            _dnxContext = dnxContext;
            _msbuildContext = msbuildContext;
            _scriptCsContext = scriptCsContext;
            _workspace = workspace;
        }

        [HttpPost("/addtoproject")]
        public void AddToProject(Request request)
        {
            if (request.FileName == null || !request.FileName.EndsWith(".cs"))
            {
                return;
            }

            var document = _workspace.GetDocument(request.FileName);

            if (document != null)
            {
                return; // file already belongs to project.
            }

            var projects = _workspace.CurrentSolution.Projects;

            var relativeProject = FindRelativeProject(request, projects);

            if (relativeProject == null || new FileInfo(relativeProject.FilePath).Extension != "csproj")
            {
                return; // Can't find relative project or csproj file.
            }

            var project = XDocument.Load(relativeProject.FilePath);

            var relativeFileName =
                new Uri(relativeProject.FilePath)
                .MakeRelativeUri(new Uri(request.FileName))
                .ToString()
                .ForceWindowsPathSeparator();

            var absoluteFileName = request.FileName.ForceWindowsPathSeparator();

            var compilationNodes = project.Element(_msBuildNameSpace + "Project")
                                          .Elements(_msBuildNameSpace + "ItemGroup")
                                          .Elements(_msBuildNameSpace + "Compile").ToList();

            var fileAlreadyInProject = compilationNodes
                .Select(n => n.Attribute("Include").Value)
                .Any(path =>
                    path.Equals(absoluteFileName, StringComparison.OrdinalIgnoreCase) ||
                    path.Equals(relativeFileName, StringComparison.OrdinalIgnoreCase));


            if (!fileAlreadyInProject)
            {
                var compilationNodeParent = compilationNodes.First().Parent;
                var newFileElement = new XElement(_msBuildNameSpace + "Compile", new XAttribute("Include", relativeFileName));
                compilationNodeParent.Add(newFileElement);
                
                File.WriteAllText(relativeProject.FilePath, project.ToString());
            }

        }

        private static Microsoft.CodeAnalysis.Project FindRelativeProject(Request request, IEnumerable<Microsoft.CodeAnalysis.Project> projects)
        {
            foreach (var project in projects)
            {
                var directory = Path.GetDirectoryName(project.FilePath);

                if (request.FileName.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }
    }
}
