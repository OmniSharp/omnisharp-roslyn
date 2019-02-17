using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Buffer;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class UpdateBufferFacts : CakeSingleRequestHandlerTestFixture<UpdateBufferHandler>
    {
        private const string CakeBuildSrc =
            @"var target = Argument(""target"", ""Default"");

Task(""Default"")
  .Does(() =>
{
  Information(""Hello World!"");
});

RunTarget(target);";
        private const string CakeBuildModified =
            @"var target = Argument(""target"", ""Foobar"");

Task(""Foobar"")
  .Does(() =>
{
  Verbose(""Hello World!"");
});

RunTarget(target);";

        public UpdateBufferFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.UpdateBuffer;

        [Fact]
        public async Task ShouldSupportIncrementalChanges()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var updateBufferRequest = new UpdateBufferRequest
                {
                    FileName = fileName,
                    Buffer = CakeBuildSrc,
                    FromDisk = false
                };
                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                updateBufferRequest = new UpdateBufferRequest
                {
                    FileName = fileName,
                    FromDisk = false,
                    Changes = new[]
                    {
                        new LinePositionSpanTextChange
                        {
                            StartLine = 0,
                            StartColumn = 33,
                            EndLine = 0,
                            EndColumn = 40,
                            NewText = "Foobar"
                        },
                        new LinePositionSpanTextChange
                        {
                            StartLine = 2,
                            StartColumn = 6,
                            EndLine = 2,
                            EndColumn = 13,
                            NewText = "Foobar"
                        },
                        new LinePositionSpanTextChange
                        {
                            StartLine = 5,
                            StartColumn = 2,
                            EndLine = 5,
                            EndColumn = 13,
                            NewText = "Verbose"
                        }
                    }
                };
                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                var sourceText = await host.Workspace.GetDocument(fileName).GetTextAsync();
                var fullPath = Path.GetFullPath(fileName).Replace('\\', '/');
                var startIndex = 0;
                for (var i = sourceText.Lines.Count - 1; i >= 0; i--)
                {
                    var text = sourceText.Lines[i].ToString();

                    if (text.Equals($"#line 1 \"{fullPath}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        startIndex = i + 1;
                        break;
                    }
                }

                var expectedLines = CakeBuildModified.Split('\n').ToList();
                for (var i = 0; i < expectedLines.Count; i++)
                {
                    Assert.Equal(sourceText.Lines[startIndex + i].ToString(), expectedLines[i].TrimEnd('\r', '\n'));
                }
            }
        }
    }
}
