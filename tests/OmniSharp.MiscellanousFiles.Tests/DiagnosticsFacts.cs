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
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filePath = testProject.AddDisposableFile("a.cs", "class C { int n = true; }");
                var service = host.GetRequestHandler<OnFilesChangedService>(OmniSharpEndpoints.FilesChanged);
                await service.Handle(new[]
                {
                    new FilesChangedRequest
                    {
                        FileName = filePath,
                        ChangeType = FileWatching.FileChangeType.Create
                    }
                });

                await Task.Delay(2000);

                var service1 = host.GetRequestHandler<CodeCheckService>(OmniSharpEndpoints.CodeCheck);
                var quickFixes = await service1.Handle(new CodeCheckRequest());

                // back off for 2 seconds to let the watcher and workspace process new projects
                Assert.Single(quickFixes.QuickFixes);
            }
        }
    }
}
