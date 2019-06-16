using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.FileWatching;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class BufferManagerFacts : AbstractTestFixture
    {
        public BufferManagerFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task UpdateBufferIgnoresVoidRequests()
        {
            using (var host = CreateOmniSharpHost(new TestFile("test.cs", "class C {}")))
            {
                Assert.Single(host.Workspace.CurrentSolution.Projects);
                Assert.Single(host.Workspace.CurrentSolution.Projects.ElementAt(0).Documents);

                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { });
                Assert.Single(host.Workspace.CurrentSolution.Projects);
                Assert.Single(host.Workspace.CurrentSolution.Projects.ElementAt(0).Documents);

                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = "", Buffer = "enum E {}" });
                Assert.Single(host.Workspace.CurrentSolution.Projects);
                Assert.Single(host.Workspace.CurrentSolution.Projects.ElementAt(0).Documents);
            }
        }

        [Fact]
        public async Task UpdateBufferIgnoresNonCsFilePathsThatDontMatchAProjectPath()
        {
            var workspace = GetWorkspaceWithProjects();

            await workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = Path.Combine("some", " path.fs"), Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(Path.Combine("some", "path.fs"));
            Assert.Empty(documents);
        }

        [Fact]
        public async Task UpdateBufferFindsProjectBasedOnPath()
        {
            var workspace = GetWorkspaceWithProjects();

            await workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = Path.Combine("src", "newFile.cs"), Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(Path.Combine("src", "newFile.cs"));
            Assert.Equal(2, documents.Count());

            foreach (var document in documents)
            {
                Assert.Equal(Path.Combine("src", "project.json"), document.Project.FilePath);
            }
        }

        [Fact]
        public async Task UpdateBufferReadsFromDisk()
        {
            const string newCode = "public class MyClass {}";

            var fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".cs";
            var testFile = new TestFile(fileName, string.Empty);
            using (var host = CreateOmniSharpHost(testFile))
            {
                File.WriteAllText(fileName, newCode);

                var request = new UpdateBufferRequest
                {
                    FileName = fileName,
                    FromDisk = true
                };

                await host.Workspace.BufferManager.UpdateBufferAsync(request);

                var document = host.Workspace.GetDocument(fileName);
                var text = await document.GetTextAsync();

                Assert.Equal(newCode, text.ToString());
            }
        }

        [Fact]
        public async Task UpdateBufferFindsProjectBasedOnNearestPath()
        {
            var workspace = new OmniSharpWorkspace(
                new HostServicesAggregator(
                    Enumerable.Empty<IHostServicesProvider>(), new LoggerFactory()),
                new LoggerFactory(), new ManualFileSystemWatcher());

            TestHelpers.AddProjectToWorkspace(workspace,
                filePath: Path.Combine("src", "root", "foo.csproj"),
                frameworks: null,
                testFiles: new[] { new TestFile(Path.Combine("src", "root", "foo.cs"), "class C1 {}") });

            TestHelpers.AddProjectToWorkspace(workspace,
                filePath: Path.Combine("src", "root", "foo", "bar", "insane.csproj"),
                frameworks: null,
                testFiles: new [] { new TestFile(Path.Combine("src", "root", "foo", "bar", "nested", "code.cs"), "class C2 {}") });

            await workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = Path.Combine("src", "root", "bar.cs"), Buffer = "enum E {}" });
            var documents = workspace.GetDocuments(Path.Combine("src", "root", "bar.cs"));
            Assert.Single(documents);
            Assert.Equal(Path.Combine("src", "root", "foo.csproj"), documents.ElementAt(0).Project.FilePath);
            Assert.Equal(2, documents.ElementAt(0).Project.Documents.Count());

            await workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = Path.Combine("src", "root", "foo", "bar", "nested", "paths", "dance.cs"), Buffer = "enum E {}" });
            documents = workspace.GetDocuments(Path.Combine("src", "root", "foo", "bar", "nested", "paths", "dance.cs"));
            Assert.Single(documents);
            Assert.Equal(Path.Combine("src", "root", "foo", "bar", "insane.csproj"), documents.ElementAt(0).Project.FilePath);
            Assert.Equal(2, documents.ElementAt(0).Project.Documents.Count());
        }

        [Fact]
        public async Task UpdateRequestHandleChanges()
        {
            var workspace = GetWorkspaceWithProjects();

            await workspace.BufferManager.UpdateBufferAsync(new Request()
            {
                FileName = Path.Combine("src", "a.cs"),
                Changes = new LinePositionSpanTextChange[]
                {
                    // class C {} -> interface C {}
                    new LinePositionSpanTextChange() {
                        StartLine = 0,
                        StartColumn = 0,
                        EndLine = 0,
                        EndColumn = 5,
                        NewText = "interface"
                    },
                    // interface C {} -> interface I {}
                    // note: this change is relative to the previous
                    // change having been applied
                    new LinePositionSpanTextChange() {
                        StartLine = 0,
                        StartColumn = 10,
                        EndLine = 0,
                        EndColumn = 11,
                        NewText = "I"
                    }
                }
            });

            var document = workspace.GetDocument(Path.Combine("src", "a.cs"));
            var text = await document.GetTextAsync();

            Assert.Equal("interface I {}", text.ToString());
        }

        private static OmniSharpWorkspace GetWorkspaceWithProjects()
        {
            var workspace = new OmniSharpWorkspace(
                new HostServicesAggregator(
                    Enumerable.Empty<IHostServicesProvider>(), new LoggerFactory()),
                new LoggerFactory(), new ManualFileSystemWatcher());

            TestHelpers.AddProjectToWorkspace(workspace,
                filePath: Path.Combine("src", "project.json"),
                frameworks: new[] { "dnx451", "dnxcore50" },
                testFiles: new [] { new TestFile(Path.Combine("src", "a.cs"), "class C {}") });

            TestHelpers.AddProjectToWorkspace(workspace,
                filePath: Path.Combine("test", "project.json"),
                frameworks: new[] { "dnx451", "dnxcore50" },
                testFiles: new [] { new TestFile(Path.Combine("test", "b.cs"), "class C {}") });

            Assert.Equal(4, workspace.CurrentSolution.Projects.Count());
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                Assert.Single(project.Documents);
            }

            return workspace;
        }
    }
}
