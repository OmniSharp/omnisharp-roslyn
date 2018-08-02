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
        public async Task TestFileAddedToMSBuildWorkspaceOnCreation()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

                var path = Path.GetDirectoryName(host.Workspace.CurrentSolution.Projects.First().FilePath);
                var filePath = Path.Combine(path, "FileName.cs");
                File.WriteAllText(filePath, "text");
                var handler = GetRequestHandler(host);
                await handler.Handle(new[] { new FilesChangedRequest() { FileName = filePath, ChangeType = FileChangeType.Create } });

                Assert.Contains(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.Name == filePath);
            }
        }

        [Fact]
        public async void TestMultipleDirectoryWatchers()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var watcher = host.GetExport<IFileSystemWatcher>();
                var filepath = testProject.AddDisposableFile("FileName.cs");
                bool firstWatcherCalled = false;
                bool secondWatcherCalled = false;
                watcher.Watch("", (path, changeType) => { firstWatcherCalled = true; });
                watcher.Watch("", (path, changeType) => { secondWatcherCalled = true; });

                var handler = GetRequestHandler(host);
                await handler.Handle(new[] { new FilesChangedRequest() { FileName = "FileName.cs", ChangeType = FileChangeType.Create } });

                Assert.True(firstWatcherCalled);
                Assert.True(secondWatcherCalled);
            }
        }

        [Fact]
        public async void TestFileExtensionWatchers()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var watcher = host.GetExport<IFileSystemWatcher>();
                var filepath = testProject.AddDisposableFile("FileName.cs");
                var extensionWatcherCalled = false;
                watcher.Watch(".cs", (path, changeType) => { extensionWatcherCalled = true; });
                var handler = GetRequestHandler(host);
                await handler.Handle(new[] { new FilesChangedRequest() { FileName = filepath, ChangeType = FileChangeType.Create } });

                Assert.True(extensionWatcherCalled);
            }
        }
    }
}
