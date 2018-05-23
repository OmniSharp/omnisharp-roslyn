using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Diagnostics;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.UpdateBuffer;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class CodeCheckFacts : CakeSingleRequestHandlerTestFixture<CodeCheckHandler>
    {
        private readonly ILogger _logger;

        public CodeCheckFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<AutoCompleteFacts>();
        }

        protected override string EndpointName => OmniSharpEndpoints.CodeCheck;

        [Fact]
        public async Task ShouldProvideDiagnosticsIfRequestContainsCakeFileName()
        {
            const string input = @"zzz";

            var diagnostics = await FindDiagnostics(input, includeFileName: true);
            Assert.NotEmpty(diagnostics.QuickFixes);
        }

        [Fact]
        public async Task ShouldNotCallCodeCheckServiceIfRequestDoesNotSpecifyFileName()
        {
            const string input = @"zzz$$";

            var diagnostics = await FindDiagnostics(input, includeFileName: false);
            Assert.Null(diagnostics);
        }

        [Fact]
        public async Task ShouldNotIncludeDiagnosticsFromLoadedFilesIfFileNameIsSpecified()
        {
            const string input = @"
#load error.cake
var target = Argument(""target"", ""Default"");";

            var diagnostics = await FindDiagnostics(input, includeFileName: true);
            Assert.Empty(diagnostics.QuickFixes);
        }

        private async Task<QuickFixResponse> FindDiagnostics(string contents, bool includeFileName)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var testFile = new TestFile(Path.Combine(testProject.Directory, "build.cake"), contents);

                var request = new CodeCheckRequest
                {
                    FileName = includeFileName ? testFile.FileName : string.Empty,
                };

                var updateBufferRequest = new UpdateBufferRequest
                {
                    Buffer = testFile.Content.Code,
                    Column = request.Column,
                    FileName = testFile.FileName,
                    Line = request.Line,
                    FromDisk = false
                };

                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                var requestHandler = GetRequestHandler(host);

                return await requestHandler.Handle(request);
            }
        }
    }
}
