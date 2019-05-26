using System.Composition;
using OmniSharp.Eventing;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;

namespace OmniSharp.Roslyn
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

        public void ProjectAnalyzedInBackground(string projectFileName, ProjectDiagnosticStatus status)
        {
            _emitter.Emit(EventTypes.ProjectDiagnosticStatus, new ProjectDiagnosticStatusMessage { ProjectFilePath = projectFileName, Status = status });
        }
    }
}
