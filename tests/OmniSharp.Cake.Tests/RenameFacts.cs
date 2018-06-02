using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Refactoring;
using OmniSharp.Models;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.Rename;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public sealed class RenameFacts : CakeSingleRequestHandlerTestFixture<RenameHandler>
    {
        public RenameFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.Rename;

        [Fact]
        public async Task ShouldSupportLoadedFiles()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var request = new RenameRequest
                {
                    FileName = fileName,
                    Line = 8,
                    Column = 10,
                    WantsTextChanges = true,
                    RenameTo = "Build"
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.Equal(2, response.Changes.Count());

                var loadedFile = response.Changes.FirstOrDefault(x => x.FileName.Equals(Path.Combine(testProject.Directory, "foo.cake")));
                Assert.NotNull(loadedFile);
                Assert.Contains(new LinePositionSpanTextChange
                    {
                        NewText = "Build",
                        StartLine = 2,
                        EndLine = 2,
                        StartColumn = 22,
                        EndColumn = 28
                    },
                    loadedFile.Changes);

                var sameFile = response.Changes.FirstOrDefault(x => x.FileName.Equals(Path.Combine(testProject.Directory, "build.cake")));
                Assert.NotNull(sameFile);
                Assert.Contains(new LinePositionSpanTextChange
                    {
                        NewText = "Build",
                        StartLine = 8,
                        EndLine = 8,
                        StartColumn = 5,
                        EndColumn = 11
                    },
                    sameFile.Changes);
            }
        }
    }
}
