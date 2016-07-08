using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.V2;
using OmniSharp.Services;

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
