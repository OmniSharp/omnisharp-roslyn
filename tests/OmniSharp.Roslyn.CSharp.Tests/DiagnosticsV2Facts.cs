using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public partial class DiagnosticsV2Facts : AbstractTestFixture
    {
        public DiagnosticsV2Facts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Theory(Skip = "Test needs to be updated for service changes")]
        [InlineData("a.cs")]
        [InlineData("a.csx")]
        public async Task CodeCheckSpecifiedFileOnly(string filename)
        {
            SharedOmniSharpTestHost.ClearWorkspace();

            var testFile = new TestFile(filename, "class C { int n = true; }");

            var emitter = new TestEventEmitter<DiagnosticMessage>();
            var forwarder = new DiagnosticEventForwarder(emitter)
            {
                IsEnabled = true
            };

            var service = CreateDiagnosticService(forwarder);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var controller = new DiagnosticsService(forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest { FileName = testFile.FileName });

            await emitter.ExpectForEmitted(msg => msg.Results.Any(m => m.FileName == filename));
        }

        private CSharpDiagnosticWorkerWithAnalyzers CreateDiagnosticService(DiagnosticEventForwarder forwarder)
        {
            return new CSharpDiagnosticWorkerWithAnalyzers(SharedOmniSharpTestHost.Workspace, Enumerable.Empty<ICodeActionProvider>(), this.LoggerFactory, forwarder, new OmniSharpOptions());
        }

        [Theory(Skip = "Test needs to be updated for service changes")]
        [InlineData("a.cs", "b.cs")]
        [InlineData("a.csx", "b.csx")]
        public async Task CheckAllFiles(string filename1, string filename2)
        {
            SharedOmniSharpTestHost.ClearWorkspace();

            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile1, testFile2);
            var emitter = new TestEventEmitter<DiagnosticMessage>();
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = CreateDiagnosticService(forwarder);

            var controller = new DiagnosticsService(forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest());

            await emitter.ExpectForEmitted(msg => msg.Results
                .Any(r => r.FileName == filename1 && r.QuickFixes.Count() == 1));
            await emitter.ExpectForEmitted(msg => msg.Results
                .Any(r => r.FileName == filename2 && r.QuickFixes.Count() == 1));
        }

        [Theory(Skip = "Test needs to be updated for service changes")]
        [InlineData("a.cs", "b.cs")]
        [InlineData("a.csx", "b.csx")]
        public async Task EnablesWhenEndPointIsHit(string filename1, string filename2)
        {
            SharedOmniSharpTestHost.ClearWorkspace();

            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile1, testFile2);

            var emitter = new TestEventEmitter<DiagnosticMessage>();
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = CreateDiagnosticService(forwarder);

            var controller = new DiagnosticsService(forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest());

            Assert.True(forwarder.IsEnabled);
        }
    }
}
