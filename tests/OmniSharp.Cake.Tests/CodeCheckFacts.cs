using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Diagnostics;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.UpdateBuffer;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace OmniSharp.Cake.Tests
{
    public class CodeCheckFacts : CakeSingleRequestHandlerTestFixture<CodeCheckHandler>
    {
        private readonly ILogger _logger;

        public CodeCheckFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<CodeCheckFacts>();
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

            // error.cake file contains code that cause error like:
            // The type or namespace name 'asdf' could not be found (are you missing a using directive or an assembly reference?) (CS0246)
            Assert.DoesNotContain(diagnostics.QuickFixes.Select(x => x.ToString()), x => x.Contains("CS0246"));
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
