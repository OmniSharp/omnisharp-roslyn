using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.Diagnostics;
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

        [Theory]
        [InlineData("a.cs", 2)]
        [InlineData("a.csx", 1)]
        public async Task CodeCheckSpecifiedFileOnly(string filename, int compilationTargetsCount)
        {
            SharedOmniSharpTestHost.ClearWorkspace();

            var testFile = new TestFile(filename, "class C { int n = true; }");

            var emitter = new DiagnosticTestEmitter();
            var forwarder = new DiagnosticEventForwarder(emitter)
            {
                IsEnabled = true
            };

            var service = CreateDiagnosticService(forwarder);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var controller = new DiagnosticsService(SharedOmniSharpTestHost.Workspace, forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest { FileName = testFile.FileName });

            await emitter.WaitForEmitted(expectedCount: compilationTargetsCount);

            Assert.Equal(compilationTargetsCount, emitter.Messages.Count());
            var message = emitter.Messages.First();
            Assert.Single(message.Results);
            var result = message.Results.First();
            Assert.Single(result.QuickFixes);
            Assert.Equal(filename, result.FileName);
        }

        private CSharpDiagnosticService CreateDiagnosticService(DiagnosticEventForwarder forwarder)
        {
            return new CSharpDiagnosticService(SharedOmniSharpTestHost.Workspace, Enumerable.Empty<ICodeActionProvider>(), this.LoggerFactory, forwarder, new RulesetsForProjects());
        }

        [Theory]
        [InlineData("a.cs", "b.cs", 2)]
        [InlineData("a.csx", "b.csx", 2)]
        public async Task CheckAllFiles(string filename1, string filename2, int compilationTargetsCount)
        {
            SharedOmniSharpTestHost.ClearWorkspace();

            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile1, testFile2);
            var emitter = new DiagnosticTestEmitter();
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = CreateDiagnosticService(forwarder);

            var controller = new DiagnosticsService(SharedOmniSharpTestHost.Workspace, forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest());

            await emitter.WaitForEmitted(expectedCount: compilationTargetsCount);

            Assert.Equal(compilationTargetsCount, emitter.Messages.Count());
            Assert.Equal(2, emitter.Messages.First().Results.Count());
            Assert.Single(emitter.Messages.First().Results.First().QuickFixes);
            Assert.Single(emitter.Messages.First().Results.Skip(1).First().QuickFixes);
        }

        [Theory]
        [InlineData("a.cs", "b.cs")]
        [InlineData("a.csx", "b.csx")]
        public async Task EnablesWhenEndPointIsHit(string filename1, string filename2)
        {
            SharedOmniSharpTestHost.ClearWorkspace();

            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile1, testFile2);
            
            var emitter = new DiagnosticTestEmitter();
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = CreateDiagnosticService(forwarder);

            var controller = new DiagnosticsService(SharedOmniSharpTestHost.Workspace, forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest());

            Assert.True(forwarder.IsEnabled);
        }
    }
}
