using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;
using OmniSharp.Workers.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public partial class DiagnosticsV2Facts : AbstractTestFixture
    {
        public DiagnosticsV2Facts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task CodeCheckSpecifiedFileOnly()
        {
            var workspace = await CreateWorkspaceAsync(
                new TestFile("a.cs", "class C { int n = true; }"));

            var messages = new List<OmniSharp.Models.DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            forwarder.IsEnabled = true;
            var service = new CSharpDiagnosticService(workspace, forwarder, this.LoggerFactory);
            service.QueueDiagnostics("a.cs");

            await emitter.Emitted;

            Assert.Equal(1, messages.Count);
            var message = messages.First();
            Assert.Equal(1, message.Results.Count());
            var result = message.Results.First();
            Assert.Equal(1, result.QuickFixes.Count());
            Assert.Equal("a.cs", result.FileName);
        }

        [Fact]
        public async Task CheckAllFiles()
        {
            var workspace = await CreateWorkspaceAsync(
                new TestFile("a.cs", "class C1 { int n = true; }"),
                new TestFile("b.cs", "class C2 { int n = true; }"));

            var messages = new List<OmniSharp.Models.DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = new CSharpDiagnosticService(workspace, forwarder, this.LoggerFactory);

            var controller = new DiagnosticsService(workspace, forwarder, service);
            var response = await controller.Handle(new OmniSharp.Models.DiagnosticsRequest());

            await emitter.Emitted;

            Assert.Equal(1, messages.Count);
            var message = messages.First();
            Assert.Equal(2, message.Results.Count());

            var a = message.Results.First(x => x.FileName == "a.cs");
            Assert.Equal(1, a.QuickFixes.Count());
            Assert.Equal("a.cs", a.FileName);

            var b = message.Results.First(x => x.FileName == "b.cs");
            Assert.Equal(1, b.QuickFixes.Count());
            Assert.Equal("b.cs", b.FileName);
        }

        [Fact]
        public async Task EnablesWhenEndPointIsHit()
        {
            var workspace = await CreateWorkspaceAsync(
                new TestFile("a.cs", "class C1 { int n = true; }"),
                new TestFile("b.cs", "class C2 { int n = true; }"));

            var messages = new List<OmniSharp.Models.DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = new CSharpDiagnosticService(workspace, forwarder, this.LoggerFactory);

            var controller = new DiagnosticsService(workspace, forwarder, service);
            var response = await controller.Handle(new OmniSharp.Models.DiagnosticsRequest());

            Assert.Equal(true, forwarder.IsEnabled);
        }
    }
}
