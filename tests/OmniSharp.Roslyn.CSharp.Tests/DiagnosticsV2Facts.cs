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
        public DiagnosticsV2Facts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("a.cs")]
        [InlineData("a.csx")]
        public async Task CodeCheckSpecifiedFileOnly(string filename)
        {
            var testFile = new TestFile(filename, "class C { int n = true; }");

            using (var host = CreateOmniSharpHost(testFile))
            {
                var messages = new List<DiagnosticMessage>();
                var emitter = new DiagnosticTestEmitter(messages);
                var forwarder = new DiagnosticEventForwarder(emitter)
                {
                    IsEnabled = true
                };

                var service = new CSharpDiagnosticService(host.Workspace, forwarder, this.LoggerFactory);
                service.QueueDiagnostics(filename);

                await emitter.Emitted;

                Assert.Equal(1, messages.Count);
                var message = messages.First();
                Assert.Equal(1, message.Results.Count());
                var result = message.Results.First();
                Assert.Equal(1, result.QuickFixes.Count());
                Assert.Equal(filename, result.FileName);
            }
        }

        [Theory]
        [InlineData("a.cs", "b.cs")]
        [InlineData("a.csx", "b.csx")]
        public async Task CheckAllFiles(string filename1, string filename2)
        {
            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");

            using (var host = CreateOmniSharpHost(testFile1, testFile2))
            {
                var messages = new List<DiagnosticMessage>();
                var emitter = new DiagnosticTestEmitter(messages);
                var forwarder = new DiagnosticEventForwarder(emitter);
                var service = new CSharpDiagnosticService(host.Workspace, forwarder, this.LoggerFactory);

                var controller = new DiagnosticsService(host.Workspace, forwarder, service);
                var response = await controller.Handle(new DiagnosticsRequest());

                await emitter.Emitted;

                Assert.Equal(1, messages.Count);
                var message = messages.First();
                Assert.Equal(2, message.Results.Count());

                var a = message.Results.First(x => x.FileName == filename1);
                Assert.Equal(1, a.QuickFixes.Count());
                Assert.Equal(filename1, a.FileName);

                var b = message.Results.First(x => x.FileName == filename2);
                Assert.Equal(1, b.QuickFixes.Count());
                Assert.Equal(filename2, b.FileName);
            }
        }

        [Theory]
        [InlineData("a.cs", "b.cs")]
        [InlineData("a.csx", "b.csx")]
        public async Task EnablesWhenEndPointIsHit(string filename1, string filename2)
        {
            var testFile1 = new TestFile(filename1, "class C1 { int n = true; }");
            var testFile2 = new TestFile(filename2, "class C2 { int n = true; }");

            using (var host = CreateOmniSharpHost(testFile1, testFile2))
            {
                var messages = new List<DiagnosticMessage>();
                var emitter = new DiagnosticTestEmitter(messages);
                var forwarder = new DiagnosticEventForwarder(emitter);
                var service = new CSharpDiagnosticService(host.Workspace, forwarder, this.LoggerFactory);

                var controller = new DiagnosticsService(host.Workspace, forwarder, service);
                var response = await controller.Handle(new DiagnosticsRequest());

                Assert.True(forwarder.IsEnabled);
            }
        }
    }
}
