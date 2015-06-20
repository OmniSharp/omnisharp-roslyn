using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Dnx;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class CurrentProjectFacts
    {
        private readonly DnxContext _context;
        private readonly OmnisharpWorkspace _workspace;

        public CurrentProjectFacts()
        {
            _context = new DnxContext();
            _workspace = new OmnisharpWorkspace();
        }

        [Fact]
        public void CanGetDnxProject()
        {
            var project1 = CreateProjectWithSourceFile("project1.json", "file1.cs");
            var project2 = CreateProjectWithSourceFile("project2.json", "file2.cs");
            var project3 = CreateProjectWithSourceFile("project3.json", "file3.cs");

            var project = GetProjectContainingSourceFile("file2.cs");

            var expectedProject = new DnxProject(project2);

            Assert.Equal(expectedProject.GlobalJsonPath, project.GlobalJsonPath);
            Assert.Equal(expectedProject.Name, project.Name);
            Assert.Equal(expectedProject.Path, project.Path);
            Assert.Equal(expectedProject.Commands.Count, project.Commands.Count);
            Assert.Equal(expectedProject.Frameworks.Count, project.Frameworks.Count);
        }

        private DnxProject GetProjectContainingSourceFile(string name)
        {
            var controller = new ProjectSystemController(_context, null, null, _workspace);

            var request = new Request
            {
                FileName = name
            };

            var response = controller.CurrentProject(request);
            return response.DnxProject;
        }

        private Dnx.Project CreateProjectWithSourceFile(string projectPath, string documentPath)
        {
            Dnx.Project project;
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
