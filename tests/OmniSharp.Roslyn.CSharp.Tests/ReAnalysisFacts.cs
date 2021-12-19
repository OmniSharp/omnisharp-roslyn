using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Abstractions.Models.V1.ReAnalyze;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;
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
        private readonly TestEventEmitter<BackgroundDiagnosticStatusMessage> _eventListener;

        public ReAnalysisFacts(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            _eventListener = new TestEventEmitter<BackgroundDiagnosticStatusMessage>();
        }


        [Fact]
        public async Task WhenReAnalyzeIsExecutedForAll_ThenReanalyzeAllFiles()
        {
            using (var host = GetHost())
            {
                var changeBufferHandler = host.GetRequestHandler<ChangeBufferService>(OmniSharpEndpoints.ChangeBuffer);
                var reAnalyzeHandler = host.GetRequestHandler<ReAnalyzeService>(OmniSharpEndpoints.ReAnalyze);

                var bContent = "public class B { }";

                host.AddFilesToWorkspace(new TestFile("a.cs", "public class A: B { }"), new TestFile("b.cs", bContent));

                await host.RequestCodeCheckAsync("a.cs");

                var newContent = "ThisDoesntContainValidReferenceAsBClassAnyMore";

                await changeBufferHandler.Handle(new ChangeBufferRequest()
                {
                    StartLine = 0,
                    StartColumn = 0,
                    EndLine = 0,
                    EndColumn = bContent.Length,
                    NewText = newContent,
                    FileName = "b.cs"
                });

                await reAnalyzeHandler.Handle(new ReAnalyzeRequest());

                var quickFixes = await host.RequestCodeCheckAsync("a.cs");

                // Reference to B is lost, a.cs should contain error about invalid reference to it.
                // error CS0246: The type or namespace name 'B' could not be found
                Assert.Contains(quickFixes.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "CS0246");
            }
        }

        [Fact]
        public async Task WhenReanalyzeIsExecuted_ThenSendEventWhenAnalysisOfProjectIsReady()
        {
            using (var host = GetHost())
            {
                var reAnalyzeHandler = host.GetRequestHandler<ReAnalyzeService>(OmniSharpEndpoints.ReAnalyze);

                var projectId = host.AddFilesToWorkspace(new TestFile("a.cs", "public class A { }")).First();
                var project =  host.Workspace.CurrentSolution.GetProject(projectId);

                _eventListener.Clear();

                await reAnalyzeHandler.Handle(new ReAnalyzeRequest());

                await _eventListener.ExpectForEmitted(x => x.NumberFilesTotal == 1 && x.Status == BackgroundDiagnosticStatus.Started);
                await _eventListener.ExpectForEmitted(x => x.NumberFilesTotal == 1 && x.Status == BackgroundDiagnosticStatus.Finished);
            }
        }

        [Fact]
        public async Task WhenReanalyzeIsExecutedForFileInProject_ThenOnlyAnalyzeProject()
        {
            using (var host = GetHost())
            {
                var reAnalyzeHandler = host.GetRequestHandler<ReAnalyzeService>(OmniSharpEndpoints.ReAnalyze);

                var projectAId = host.AddFilesToWorkspace(new TestFile("a.cs", "public class A { }")).First();
                var projectA =  host.Workspace.CurrentSolution.GetProject(projectAId);

                _eventListener.Clear();

                await reAnalyzeHandler.Handle(new ReAnalyzeRequest
                {
                    FileName = projectA.Documents.Single(x => x.FilePath.EndsWith("a.cs")).FilePath
                });

                await _eventListener.ExpectForEmitted(x => x.NumberFilesTotal == 1 && x.Status == BackgroundDiagnosticStatus.Started);
                await _eventListener.ExpectForEmitted(x => x.NumberFilesTotal == 1 && x.Status == BackgroundDiagnosticStatus.Finished);
            }
        }

        [Fact]
        public async Task WhenCurrentFileIsProjectItself_ThenReAnalyzeItAsExpected()
        {
            using (var host = GetHost())
            {
                var reAnalyzeHandler = host.GetRequestHandler<ReAnalyzeService>(OmniSharpEndpoints.ReAnalyze);

                var projectId = host.AddFilesToWorkspace(new TestFile("a.cs", "public class A { }")).First();
                var project =  host.Workspace.CurrentSolution.GetProject(projectId);

                _eventListener.Clear();

                await reAnalyzeHandler.Handle(new ReAnalyzeRequest
                {
                    FileName = project.FilePath
                });

                await _eventListener.ExpectForEmitted(x => x.NumberFilesTotal == 1 && x.Status == BackgroundDiagnosticStatus.Started);
                await _eventListener.ExpectForEmitted(x => x.NumberFilesTotal == 1 && x.Status == BackgroundDiagnosticStatus.Finished);
            }
        }

        private OmniSharpTestHost GetHost()
        {
            return OmniSharpTestHost.Create(testOutput: _testOutput,
                configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true), eventEmitter: _eventListener);
        }
    }
}
