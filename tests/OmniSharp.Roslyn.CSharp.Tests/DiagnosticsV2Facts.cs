using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Workers.Diagnostics;
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
            var testFile = new TestFile(filename, "class C { int n = true; }");

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var messages = new List<DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter)
            {
                IsEnabled = true
            };

            var service = new CSharpDiagnosticService(SharedOmniSharpTestHost.Workspace, forwarder, this.LoggerFactory);
            service.QueueDiagnostics(filename);

            await emitter.Emitted;

            Assert.Single(messages);
            var message = messages.First();
            Assert.Single(message.Results);
            var result = message.Results.First();
            Assert.Single(result.QuickFixes);
            Assert.Equal(filename, result.FileName);
        }

        [Theory(Skip = "Test needs to be updated for service changes")]
        [InlineData("a.cs", "b.cs")]
        [InlineData("a.csx", "b.csx")]
        public async Task CheckAllFiles(string filename1, string filename2)
        {
            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile1, testFile2);
            var messages = new List<DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = new CSharpDiagnosticService(SharedOmniSharpTestHost.Workspace, forwarder, this.LoggerFactory);

            var controller = new DiagnosticsService(SharedOmniSharpTestHost.Workspace, forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest());

            await emitter.Emitted;

            Assert.Single(messages);
            var message = messages.First();
            Assert.Equal(2, message.Results.Count());

            var a = message.Results.First(x => x.FileName == filename1);
            Assert.Single(a.QuickFixes);
            Assert.Equal(filename1, a.FileName);

            var b = message.Results.First(x => x.FileName == filename2);
            Assert.Single(b.QuickFixes);
            Assert.Equal(filename2, b.FileName);
        }

        [Theory(Skip = "Test needs to be updated for service changes")]
        [InlineData("a.cs", "b.cs")]
        [InlineData("a.csx", "b.csx")]
        public async Task EnablesWhenEndPointIsHit(string filename1, string filename2)
        {
            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile1, testFile2);
            var messages = new List<DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = new CSharpDiagnosticService(SharedOmniSharpTestHost.Workspace, forwarder, this.LoggerFactory);

            var controller = new DiagnosticsService(SharedOmniSharpTestHost.Workspace, forwarder, service);
            var response = await controller.Handle(new DiagnosticsRequest());

            Assert.True(forwarder.IsEnabled);
        }
    }
}
