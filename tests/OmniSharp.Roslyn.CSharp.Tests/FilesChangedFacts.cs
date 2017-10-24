using OmniSharp.FileWatching;
using OmniSharp.Models;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Roslyn.CSharp.Services.Files;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OnFilesChangedFacts : AbstractSingleRequestHandlerTestFixture<OnFilesChangedService>
    {
        protected OnFilesChangedFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.FilesChanged;

        [Fact]
        public void TestFileWatcherCalled()
        {
            var host = CreateEmptyOmniSharpHost();

            var watcher = host.GetExport<IFileSystemWatcher>();

            string filePath = null;
            FileChangeType? ct = null;
            watcher.WatchDirectory("", (path, changeType) => { filePath = path; ct = changeType; });


            var handler = GetRequestHandler(host);
            handler.Handle(new[] { new FilesChangedRequest() { FileName = "FileName.cs", FileChangeType = FileChangeType.Create } });

            Assert.Equal("FileName.cs", filePath);
            Assert.Equal(FileChangeType.Create, ct);

        }
    }
}
