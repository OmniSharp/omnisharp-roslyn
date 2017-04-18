using System.Composition;
using OmniSharp.Models;
using OmniSharp.Models.Diagnostics;

namespace OmniSharp.Services
{
    [Export, Shared]
    public class DiagnosticEventForwarder
    {
        private readonly IEventEmitter _emitter;

        [ImportingConstructor]
        public DiagnosticEventForwarder(IEventEmitter emitter)
        {
            _emitter = emitter;
        }

        public bool IsEnabled { get; set; }

        public void Forward(DiagnosticMessage message)
        {
            _emitter.Emit(EventTypes.Diagnostic, message);
        }
    }
}
