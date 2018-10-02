using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.FileWatching;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Roslyn.CSharp.Services.Files;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OnFilesChangedFacts : AbstractSingleRequestHandlerTestFixture<OnFilesChangedService>
    {
        public OnFilesChangedFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.FilesChanged;

        [Fact]
        public async Task TestFileAddedToMSBuildWorkspaceOnCreationAndRemovedOnDeletion()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

                var path = Path.GetDirectoryName(host.Workspace.CurrentSolution.Projects.First().FilePath);
                var filePath = Path.Combine(path, "FileName.cs");
                var handler = GetRequestHandler(host);

                // test add
                File.WriteAllText(filePath, "text");
                await handler.Handle(new[] { new FilesChangedRequest() { FileName = filePath, ChangeType = FileChangeType.Create } });
                Assert.Contains(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.Name == filePath);

                // test delete
                File.Delete(filePath);
                await handler.Handle(new[] { new FilesChangedRequest() { FileName = filePath, ChangeType = FileChangeType.Delete } });
                Assert.DoesNotContain(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.Name == filePath);

                // second creation on the same file path. Checks if omnisharp properly removed deleted document.
                File.WriteAllText(filePath, "text");
                await handler.Handle(new[] { new FilesChangedRequest() { FileName = filePath, ChangeType = FileChangeType.Create } });
                Assert.Contains(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.Name == filePath);
            }
        }

        [Fact]
        public void TestMultipleDirectoryWatchers()
        {
            using (var host = CreateEmptyOmniSharpHost())
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

                bool firstWatcherCalled = false;
                bool secondWatcherCalled = false;
                watcher.Watch("", (path, changeType) => { firstWatcherCalled = true; });
                watcher.Watch("", (path, changeType) => { secondWatcherCalled = true; });

                var handler = GetRequestHandler(host);
                handler.Handle(new[] { new FilesChangedRequest() { FileName = "FileName.cs", ChangeType = FileChangeType.Create } });

                Assert.True(firstWatcherCalled);
                Assert.True(secondWatcherCalled);
            }
        }

        [Fact]
        public void TestFileExtensionWatchers()
        {
            using (var host = CreateEmptyOmniSharpHost())
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

                var extensionWatcherCalled = false;
                watcher.Watch(".cs", (path, changeType) => { extensionWatcherCalled = true; });

                var handler = GetRequestHandler(host);
                handler.Handle(new[] { new FilesChangedRequest() { FileName = "FileName.cs", ChangeType = FileChangeType.Create } });

                Assert.True(extensionWatcherCalled);
            }
        }
    }
}
