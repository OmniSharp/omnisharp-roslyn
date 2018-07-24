using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Files;
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
        public async Task Returns_only_semantic_diagnotics()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filePath = testProject.AddDisposableFile("a.cs", "class C { b a = new b(); int n  }");
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

                var codeCheckService = host.GetRequestHandler<CodeCheckService>(OmniSharpEndpoints.CodeCheck);
                var quickFixes = await codeCheckService.Handle(new CodeCheckRequest());

                Assert.Single(quickFixes.QuickFixes);
                Assert.Equal("; expected", quickFixes.QuickFixes.First().Text);
            }
        }
    }
}
