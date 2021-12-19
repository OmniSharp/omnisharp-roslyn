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

        public void BackgroundDiagnosticsStatus(BackgroundDiagnosticStatus status, int numberProjects, int numberFiles, int numberFilesRemaining)
        {
            // New type of background diagnostic event, allows more control of visualization in clients:
            _emitter.Emit(EventTypes.BackgroundDiagnosticStatus, new BackgroundDiagnosticStatusMessage
            {
                Status = status,
                NumberProjects = numberProjects,
                NumberFilesTotal = numberFiles,
                NumberFilesRemaining = numberFilesRemaining
            });

            // Old type of event emitted as a shim for older clients:
            double percentComplete = 0;
            if (numberFiles > 0 && numberFiles > numberFilesRemaining)
            {
                percentComplete = numberFiles <= 0
                    ? 100
                    : (numberFiles - numberFilesRemaining) / (double)numberFiles;
            }

            _emitter.Emit(EventTypes.ProjectDiagnosticStatus, new ProjectDiagnosticStatusMessage
            {
                // There is no current project file being analyzed anymore since all the analysis
                // executes concurrently, but we have to supply some value for the ProjectFilePath
                // property for clients that only know about this event. In VS Code the following
                // displays nicely as "Analyzing (24%)".
                ProjectFilePath = $"({percentComplete:P0})",
                Status = status == BackgroundDiagnosticStatus.Finished ?
                    ProjectDiagnosticStatus.Ready :
                    ProjectDiagnosticStatus.Started
            });
        }
    }
}
