using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.AspNet5;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class CurrentProjectFacts
    {
        private readonly AspNet5Context _context;
        private readonly OmnisharpWorkspace _workspace;

        public CurrentProjectFacts()
        {
            _context = new AspNet5Context();
            _workspace = new OmnisharpWorkspace();
        }

        [Fact]
        public void CanGetAspNet5Project()
        {
            var project1 = CreateProjectWithSourceFile("project1.json", "file1.cs");
            var project2 = CreateProjectWithSourceFile("project2.json", "file2.cs");
            var project3 = CreateProjectWithSourceFile("project3.json", "file3.cs");

            var project = GetProjectContainingSourceFile("file2.cs");

            var expectedProject = new AspNet5Project(project2);

            Assert.Equal(expectedProject.GlobalJsonPath, project.GlobalJsonPath);
            Assert.Equal(expectedProject.Name, project.Name);
            Assert.Equal(expectedProject.Path, project.Path);
            Assert.Equal(expectedProject.Commands.Count, project.Commands.Count);
            Assert.Equal(expectedProject.Frameworks.Count, project.Frameworks.Count);
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

        private AspNet5.Project CreateProjectWithSourceFile(string projectPath, string documentPath)
        {
            AspNet5.Project project;
            _context.TryAddProject(projectPath, out project);
            var projectId = ProjectId.CreateNewId();
            var versionStamp = VersionStamp.Create();
            var projectInfo = ProjectInfo.Create(projectId, versionStamp,
                                                 "ProjectName", "AssemblyName",
                                                 LanguageNames.CSharp, projectPath);

            var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), documentPath, loader: TextLoader.From(TextAndVersion.Create(SourceText.From(""), versionStamp)), filePath: documentPath);

            _workspace.AddProject(projectInfo);
            _workspace.AddDocument(document);
            return project;
        }
    }
}
