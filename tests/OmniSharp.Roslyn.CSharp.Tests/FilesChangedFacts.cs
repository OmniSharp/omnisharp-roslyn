﻿using System.IO;
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

                var filePath = await AddFile(host);

                Assert.Contains(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.FilePath == filePath && d.Name == "FileName.cs");
            }
        }

        [Fact]
        public async Task TestFileMovedToPreviouslyEmptyDirectory()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

                var projectDirectory = Path.GetDirectoryName(host.Workspace.CurrentSolution.Projects.First().FilePath);

                var filePath = await AddFile(host);

                Assert.Contains(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.FilePath == filePath && d.Name == "FileName.cs");

                var nestedDirectory = Path.Combine(projectDirectory, "Nested");
                Directory.CreateDirectory(nestedDirectory);

                var destinationPath = Path.Combine(nestedDirectory, Path.GetFileName(filePath));
                File.Move(filePath, destinationPath);

                await GetRequestHandler(host).Handle(new[] { new FilesChangedRequest() { FileName = filePath, ChangeType = FileChangeType.Delete } });
                await GetRequestHandler(host).Handle(new[] { new FilesChangedRequest() { FileName = destinationPath, ChangeType = FileChangeType.Create } });

                Assert.Contains(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.FilePath == destinationPath && d.Name == "FileName.cs");
                Assert.DoesNotContain(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.FilePath == filePath && d.Name == "FileName.cs");
            }
        }

        [Fact]
        public async Task TestMultipleDirectoryWatchers()
        {
            using (var host = CreateEmptyOmniSharpHost())
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

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
        public async Task TestFileExtensionWatchers()
        {
            using (var host = CreateEmptyOmniSharpHost())
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

                var extensionWatcherCalled = false;
                watcher.Watch(".cs", (path, changeType) => { extensionWatcherCalled = true; });

                var handler = GetRequestHandler(host);
                await handler.Handle(new[] { new FilesChangedRequest() { FileName = "FileName.cs", ChangeType = FileChangeType.Create } });

                Assert.True(extensionWatcherCalled);
            }
        }

        [Fact]
        // This is specifically added to workaround VScode broken file remove notifications on folder removals/moves/renames.
        // It's by design at VsCode and will probably not get fixed any time soon if ever.
        public async Task TestThatOnFolderRemovalFilesUnderFolderAreRemoved()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var watcher = host.GetExport<IFileSystemWatcher>();

                var filePath = await AddFile(host);

                await GetRequestHandler(host).Handle(new[] { new FilesChangedRequest() { FileName = Path.GetDirectoryName(filePath), ChangeType = FileChangeType.DirectoryDelete } });

                Assert.DoesNotContain(host.Workspace.CurrentSolution.Projects.First().Documents, d => d.FilePath == filePath && d.Name == Path.GetFileName(filePath));
            }
        }

        private async Task<string> AddFile(OmniSharpTestHost host)
        {
            var projectDirectory = Path.GetDirectoryName(host.Workspace.CurrentSolution.Projects.First().FilePath);
            const string filename = "FileName.cs";
            var filePath = Path.Combine(projectDirectory, filename);
            File.WriteAllText(filePath, "text");
            await GetRequestHandler(host).Handle(new[] { new FilesChangedRequest() { FileName = filePath, ChangeType = FileChangeType.Create } });
            return filePath;
        }
    }
}
