using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class BufferManagerFacts
    {
        [Fact]
        public void UpdateBufferIgnoresVoidRequests()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace("class C {}", "test.cs");
            Assert.Equal(2, workspace.CurrentSolution.Projects.Count());
            Assert.Equal(1, workspace.CurrentSolution.Projects.ElementAt(0).Documents.Count());
            Assert.Equal(1, workspace.CurrentSolution.Projects.ElementAt(1).Documents.Count());

            workspace.BufferManager.UpdateBuffer(new Request() { });
            Assert.Equal(2, workspace.CurrentSolution.Projects.Count());
            Assert.Equal(1, workspace.CurrentSolution.Projects.ElementAt(0).Documents.Count());
            Assert.Equal(1, workspace.CurrentSolution.Projects.ElementAt(1).Documents.Count());

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = "", Buffer = "enum E {}" });
            Assert.Equal(2, workspace.CurrentSolution.Projects.Count());
            Assert.Equal(1, workspace.CurrentSolution.Projects.ElementAt(0).Documents.Count());
            Assert.Equal(1, workspace.CurrentSolution.Projects.ElementAt(1).Documents.Count());
        }

        [Fact]
        public void UpdateBufferIgnoresFilePathsThatDontMatchAProjectPath()
        {
            var workspace = GetWorkspaceWithProjects();

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = Path.Combine("some", " path.cs"), Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(Path.Combine("some", "path.cs"));
            Assert.Equal(0, documents.Count());
        }

        [Fact]
        public void UpdateBufferFindsProjectBasedOnPath()
        {
            var workspace = GetWorkspaceWithProjects();

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = Path.Combine("src", "newFile.cs"), Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(Path.Combine("src", "newFile.cs"));
            Assert.Equal(2, documents.Count());

            foreach (var document in documents)
            {
                Assert.Equal(Path.Combine("src", "project.json"), document.Project.FilePath);
            }
        }

        [Fact]
        public void UpdateBufferFindsProjectBasedOnNearestPath()
        {
            var workspace = new OmnisharpWorkspace();


            TestHelpers.AddProjectToWorkspace(workspace, Path.Combine("src", "root", "foo.csproj"),
                new[] { "" },
                new Dictionary<string, string>() { { Path.Combine("src", "root", "foo.cs"), "class C1 {}" } });

            TestHelpers.AddProjectToWorkspace(workspace, Path.Combine("src", "root", "foo", "bar", "insane.csproj"),
                new[] { "" },
                new Dictionary<string, string>() { { Path.Combine("src", "root", "foo", "bar", "nested", "code.cs"), "class C2 {}" } });

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = Path.Combine("src", "root", "bar.cs"), Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(Path.Combine("src", "root", "bar.cs"));
            Assert.Equal(1, documents.Count());
            Assert.Equal(Path.Combine("src", "root", "foo.csproj"), documents.ElementAt(0).Project.FilePath);
            Assert.Equal(2, documents.ElementAt(0).Project.Documents.Count());

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = Path.Combine("src", "root", "foo", "bar", "nested", "paths", "dance.cs"), Buffer = "enum E {}" });
            documents = workspace.GetDocuments(Path.Combine("src", "root", "foo", "bar", "nested", "paths", "dance.cs"));
            Assert.Equal(1, documents.Count());
            Assert.Equal(Path.Combine("src", "root", "foo", "bar", "insane.csproj"), documents.ElementAt(0).Project.FilePath);
            Assert.Equal(2, documents.ElementAt(0).Project.Documents.Count());
        }

        private static OmnisharpWorkspace GetWorkspaceWithProjects()
        {
            var workspace = new OmnisharpWorkspace();

            TestHelpers.AddProjectToWorkspace(workspace, Path.Combine("src", "project.json"),
                new[] { "dnx451", "dnxcore50" },
                new Dictionary<string, string>() { { Path.Combine("src", "a.cs"), "class C {}" } });

            TestHelpers.AddProjectToWorkspace(workspace, Path.Combine("test", "project.json"),
                new[] { "dnx451", "dnxcore50" },
                new Dictionary<string, string>() { { Path.Combine("test", "b.cs"), "class C {}" } });

            Assert.Equal(4, workspace.CurrentSolution.Projects.Count());
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                Assert.Equal(1, project.Documents.Count());
            }

            return workspace;
        }
    }
}
