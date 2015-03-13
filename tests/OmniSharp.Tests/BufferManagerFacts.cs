using System.Collections.Generic;
using System.Linq;
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

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = @"c:\some\path.cs", Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(@"c:\some\path.cs");
            Assert.Equal(0, documents.Count());
        }

        [Fact]
        public void UpdateBufferFindsProjectBasedOnPath()
        {
            var workspace = GetWorkspaceWithProjects();

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = @"c:\src\newFile.cs", Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(@"c:\src\newFile.cs");
            Assert.Equal(2, documents.Count());

            foreach (var document in documents)
            {
                Assert.Equal(@"c:\src\project.json", document.Project.FilePath);
            }
        }

        [Fact]
        public void UpdateBufferFindsProjectBasedOnNearestPath()
        {
            var workspace = new OmnisharpWorkspace();

            TestHelpers.AddProjectToWorkspace(workspace, @"c:\src\root\foo.csproj",
                new[] { "" },
                new Dictionary<string, string>() { { @"c:\src\root\foo.cs", "class C1 {}" } });

            TestHelpers.AddProjectToWorkspace(workspace, @"c:\src\root\foo\bar\insane.csproj",
                new[] { "" },
                new Dictionary<string, string>() { { @"c:\src\root\foo\bar\nested\code.cs", "class C2 {}" } });

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = @"c:\src\root\bar.cs", Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(@"c:\src\root\bar.cs");
            Assert.Equal(1, documents.Count());
            Assert.Equal(@"c:\src\root\foo.csproj", documents.ElementAt(0).Project.FilePath);
            Assert.Equal(2, documents.ElementAt(0).Project.Documents.Count());

            workspace.BufferManager.UpdateBuffer(new Request() { FileName = @"c:\src\root\foo\bar\nested\paths\dance.cs", Buffer = "enum E {}" });
            documents = workspace.GetDocuments(@"c:\src\root\foo\bar\nested\paths\dance.cs");
            Assert.Equal(1, documents.Count());
            Assert.Equal(@"c:\src\root\foo\bar\insane.csproj", documents.ElementAt(0).Project.FilePath);
            Assert.Equal(2, documents.ElementAt(0).Project.Documents.Count());
        }

        private static OmnisharpWorkspace GetWorkspaceWithProjects()
        {
            var workspace = new OmnisharpWorkspace();

            TestHelpers.AddProjectToWorkspace(workspace, @"c:\src\project.json",
                new[] { "aspnet50", "aspnet50core" },
                new Dictionary<string, string>() { { @"c:\src\a.cs", "class C {}" } });

            TestHelpers.AddProjectToWorkspace(workspace, @"c:\test\project.json",
                new[] { "aspnet50", "aspnet50core" },
                new Dictionary<string, string>() { { @"c:\test\b.cs", "class C {}" } });

            Assert.Equal(4, workspace.CurrentSolution.Projects.Count());
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                Assert.Equal(1, project.Documents.Count());
            }

            return workspace;
        }
    }
}