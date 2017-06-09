using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Eventing;
using OmniSharp.Models.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public partial class DiagnosticsV2Facts
    {
        private class DiagnosticTestEmitter : IEventEmitter
        {
            private readonly IList<DiagnosticMessage> _messages;
            private readonly TaskCompletionSource<object> _tcs;

            public Task Emitted => _tcs.Task;

            public DiagnosticTestEmitter(IList<DiagnosticMessage> messages)
            {
                _messages = messages;
                _tcs = new TaskCompletionSource<object>();
            }

            public void Emit(string kind, object args)
            {
                _messages.Add((DiagnosticMessage)args);
                _tcs.TrySetResult(null);
            }
        }
    }
}
