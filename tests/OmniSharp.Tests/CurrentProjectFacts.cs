using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.AspNet5;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class CurrentProjectFacts
    {
        AspNet5Context _context;
        OmnisharpWorkspace _workspace;
        // int _projectCounter;

        public CurrentProjectFacts()
        {
            _context = new AspNet5Context();
            _workspace = new OmnisharpWorkspace();
            // _projectCounter = 1;
        }

        [Fact]
        public void CanGetAspNet5Project()
        {
            var project1 = CreateProjectWithSourceFile("project1.json", "file1.cs");
            var project2 = CreateProjectWithSourceFile("project2.json", "file2.cs");
            var project3 = CreateProjectWithSourceFile("project3.json", "file3.cs");

            var project = GetProjectContainingSourceFile("file2.cs");
            Assert.Same(new AspNet5Project(project2), project);
        }

        private AspNet5Project GetProjectContainingSourceFile(string name)
        {
            var controller = new ProjectSystemController(_context, null, _workspace);

            var request = new Request
            {
                FileName = name
            };

            var response = controller.CurrentProject(request);
            return response.AspNet5Project;
        }

        private AspNet5.Project GetProject(string projectPath)
        {
            return new AspNet5.Project
            {
                Name = "OmniSharp",
                Path = projectPath,
                Commands = { { "kestrel", "Microsoft.AspNet.Hosting --server Kestrel" } }
            };
        }

        private AspNet5.Project CreateProjectWithSourceFile(string projectPath, string documentPath)
        {
            AspNet5.Project project;
            _context.TryAddProject(projectPath, out project);
            // var project = GetProject(projectPath);
            var projectId = ProjectId.CreateNewId();
            var versionStamp = VersionStamp.Create();
            var projectInfo = ProjectInfo.Create(projectId, versionStamp,
                                                 "ProjectName", "AssemblyName",
                                                 LanguageNames.CSharp, projectPath);

            var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), documentPath, loader: TextLoader.From(TextAndVersion.Create(SourceText.From(""), versionStamp)), filePath: documentPath);

            _workspace.AddProject(projectInfo);
            _workspace.AddDocument(document);
            // _context.Projects.Add(_projectCounter, project);
            // _context.ProjectContextMapping.Add(project.Path, _projectCounter);
            // _projectCounter++;
            // return project;
            return project;
        }

        /*

        private string GetProjectName(string source = "")
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var aspnet5Context = GetAspNet5Context();
            var controller = new ProjectSystemController(aspnet5Context, null, workspace);
            var request = CreateRequest(source);
            var response = controller.CurrentProject(request);
            return response.AspNet5Project.Name;
        }
        
        private string GetPath(string source = "")
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var aspnet5Context = GetAspNet5Context();
            var controller = new ProjectSystemController(aspnet5Context, null, workspace);
            var request = CreateRequest(source);
            var response = controller.CurrentProject(request);
            return response.AspNet5Project.Path;
        }
        
        private IDictionary<string, string> GetCommands(string source = "")
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var aspnet5Context = GetAspNet5Context();
            var controller = new ProjectSystemController(aspnet5Context, null, workspace);
            var request = CreateRequest(source);
            var response = controller.CurrentProject(request);
            return response.AspNet5Project.Commands;
        }

        private AspNet5Context GetAspNet5Context()
        {
            var context = new AspNet5Context();
            var projectCounter = 1;
            context.Projects.Add(projectCounter, new AspNet5.Project
            {
                Name = "OmniSharp",
                Path = "project.json",
                Commands = { { "kestrel", "Microsoft.AspNet.Hosting --server Kestrel" } }
            });
            context.ProjectContextMapping.Add("project.json", 1);

            return context;
        }

        private Request CreateRequest(string source, string fileName = "dummy.cs")
        {
            return new Request
            {
                Line = 0,
                Column = 0,
                FileName = fileName,
                Buffer = source
            };
        }
        */
    }
}
