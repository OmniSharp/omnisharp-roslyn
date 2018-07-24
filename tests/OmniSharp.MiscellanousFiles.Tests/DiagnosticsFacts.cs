using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.SignatureHelp;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Files;
using OmniSharp.Roslyn.CSharp.Services.Signatures;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MiscellanousFiles.Tests
{
    public class DiagnosticsFacts : AbstractTestFixture
    {
        public DiagnosticsFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Returns_only_syntactic_diagnotics()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                var testFile = new TestFile("a.cs", "class C { b a = new b(); int n  }");
                using (var host = await AddMiscellanousFile(testProject, testFile))
                {
                    var codeCheckService = host.GetRequestHandler<CodeCheckService>(OmniSharpEndpoints.CodeCheck);
                    var quickFixes = await codeCheckService.Handle(new CodeCheckRequest());
                    Assert.Single(quickFixes.QuickFixes);
                    Assert.Equal("; expected", quickFixes.QuickFixes.First().Text);
                }
            }
        }

        [Fact]
        public async Task Returns_signature_help()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                const string source =
@"class Program
{
    public static void Main(){
        System.Guid.NewGuid($$);
    }
}";
                var testFile = new TestFile("a.cs", source);
                using (var host = await AddMiscellanousFile(testProject, testFile))
                {
                    var service = host.GetRequestHandler<SignatureHelpService>(OmniSharpEndpoints.SignatureHelp);
                    var point = testFile.Content.GetPointFromPosition();
                    var request = new SignatureHelpRequest()
                    {
                        FileName = testFile.FileName,
                        Line = point.Line,
                        Column = point.Offset,
                        Buffer = testFile.Content.Code
                    };

                    var actual = await service.Handle(request);
                    Assert.Single(actual.Signatures);
                    Assert.Equal(0, actual.ActiveParameter);
                    Assert.Equal(0, actual.ActiveSignature);
                    Assert.Equal("NewGuid", actual.Signatures.ElementAt(0).Name);
                    Assert.Empty(actual.Signatures.ElementAt(0).Parameters);
                }
            }
        }

        private async Task<OmniSharpTestHost> AddMiscellanousFile(ITestProject testProject, TestFile testfile)
        {
            var host = CreateOmniSharpHost(testProject.Directory);
            var filePath = testProject.AddDisposableFile(testfile.FileName, testfile.Content.Text.ToString());
            var fileChangedService = host.GetRequestHandler<OnFilesChangedService>(OmniSharpEndpoints.FilesChanged);
            await fileChangedService.Handle(new[]
            {
                    new FilesChangedRequest
                    {
                        FileName = filePath,
                        ChangeType = FileWatching.FileChangeType.Create
                    }
                });

            await Task.Delay(2000);
            return host;
        }
    }
}
