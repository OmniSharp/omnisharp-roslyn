using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Razor.Services;
using OmniSharp.Razor.Workers.Diagnostics;
using OmniSharp.Services;
using OmniSharp.Tests;
using OmniSharp.Workers.Diagnostics;
using TestUtility.Fake;
using Xunit;

namespace OmniSharp.Razor.Tests.Workers
{
    class DiagnosticTestEmitter : IEventEmitter
    {
        private readonly IList<OmniSharp.Models.DiagnosticMessage> _messages;
        private readonly TaskCompletionSource<object> _tcs;
        public Task Emitted { get { return _tcs.Task; } }
        public DiagnosticTestEmitter(IList<OmniSharp.Models.DiagnosticMessage> messages)
        {
            _messages = messages;
            _tcs = new TaskCompletionSource<object>();
            Task.Run(async () => {
                await Task.Delay(TimeSpan.FromMilliseconds(30000));
                _tcs.TrySetResult(null);
            });
        }
        public void Emit(string kind, object args)
        {
            _messages.Add((OmniSharp.Models.DiagnosticMessage)args);
            _tcs.TrySetResult(null);
        }
    }

    public class RazorDiagnosticsFacts
    {
        [Fact]
        public async Task CodeCheckSpecifiedFileOnly()
        {
            var result = await RazorTestHelpers.CreateTestWorkspace(
                "RazorProjectSample01",
                new Dictionary<string, string>
            {
                { "a.cshtml", "@model string;\n <h1>@tile</h1>" },
                { "b.cshtml", "@model string;\n <h1>@itle</h1>" },
            });
            var workspace = result.OmnisharpWorkspace;

            var fakeLoggerFactory = new FakeLoggerFactory();
            var messages = new List<OmniSharp.Models.DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            forwarder.IsEnabled = true;
            var service = new RazorDiagnosticService(workspace, result.RazorWorkspace, forwarder, fakeLoggerFactory);
            service.QueueDiagnostics("a.cshtml");

            await emitter.Emitted;

            Assert.Equal(1, messages.Count);
            var message = messages.First();
            Assert.Equal(1, message.Results.Count());
            var r = message.Results.First();
            Assert.Equal(1, r.QuickFixes.Count());
            Assert.Equal("a.cshtml", r.FileName);
        }

        [Fact]
        public async Task CheckAllFiles()
        {
            var result = await RazorTestHelpers.CreateTestWorkspace("RazorProjectSample01");
            var workspace = result.OmnisharpWorkspace;

            var fakeLoggerFactory = new FakeLoggerFactory();
            var messages = new List<OmniSharp.Models.DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = new RazorDiagnosticService(workspace, result.RazorWorkspace, forwarder, fakeLoggerFactory);

            var controller = new DiagnosticsService(workspace, forwarder, service);
            var response = await controller.Handle(new OmniSharp.Models.DiagnosticsRequest());

            await emitter.Emitted;

            Assert.Equal(1, messages.Count);
            var message = messages.First();
            Assert.Equal(2, message.Results.Count());

            var a = message.Results.First(x => x.FileName == "Test.cshtml");
            Assert.Equal(1, a.QuickFixes.Count());
            Assert.Equal("Test.cshtml", a.FileName);
        }


        [Fact]
        public async Task EnablesWhenEndPointIsHit()
        {
            var result = await RazorTestHelpers.CreateTestWorkspace(
                "RazorProjectSample01",
                new Dictionary<string, string>
            {
                { "a.cshtml", "@model string;\n <h1>@tile</h1>" },
                { "b.cshtml", "@model string;\n <h1>@itle</h1>" },
            });
            var workspace = result.OmnisharpWorkspace;

            var fakeLoggerFactory = new FakeLoggerFactory();
            var messages = new List<OmniSharp.Models.DiagnosticMessage>();
            var emitter = new DiagnosticTestEmitter(messages);
            var forwarder = new DiagnosticEventForwarder(emitter);
            var service = new RazorDiagnosticService(workspace, result.RazorWorkspace, forwarder, fakeLoggerFactory);

            var controller = new DiagnosticsService(workspace, forwarder, service);
            var response = await controller.Handle(new OmniSharp.Models.DiagnosticsRequest());

            Assert.Equal(true, forwarder.IsEnabled);
        }
    }
}
