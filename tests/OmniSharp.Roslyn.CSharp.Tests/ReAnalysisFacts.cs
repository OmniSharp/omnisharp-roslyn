using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Abstractions.Models.V1.ReAnalyze;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Roslyn.CSharp.Services.Buffer;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class ReAnalysisFacts
    {
private readonly ITestOutputHelper _testOutput;

        public ReAnalysisFacts(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }


        [Fact]
        public async Task WhenReAnalyzeIsExecutedForAll_ThenReanalyzeAllFiles()
        {
            using (var host = GetHost())
            {
                var changeBufferHandler = host.GetRequestHandler<ChangeBufferService>(OmniSharpEndpoints.ChangeBuffer);
                var reAnalyzeHandler = host.GetRequestHandler<ReAnalyzeService>(OmniSharpEndpoints.ReAnalyze);

                host.AddFilesToWorkspace(new TestFile("a.cs", "public class A: B { }"), new TestFile("b.cs", "public class B { }"));

                await host.RequestCodeCheckAsync("a.cs");

                var newContent = "ThisDoesntContainValidReferenceAsBClassAnyMore";

                await changeBufferHandler.Handle(new ChangeBufferRequest()
                {
                    StartLine = 0,
                    StartColumn = 0,
                    EndLine = 0,
                    EndColumn = newContent.Length,
                    NewText = newContent,
                    FileName = "b.cs"
                });

                await reAnalyzeHandler.Handle(new ReAnalyzeRequest());

                var quickFixes = await host.RequestCodeCheckAsync("a.cs");

                // Reference to B is lost, a.cs should contain error about invalid reference to it.
                // error CS0246: The type or namespace name 'B' could not be found
                Assert.Contains(quickFixes.QuickFixes.Select(x => x.ToString()), x => x.Contains("CS0246"));
            }
        }

        private OmniSharpTestHost GetHost()
        {
            return OmniSharpTestHost.Create(testOutput: _testOutput,
                configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));
        }
    }
}