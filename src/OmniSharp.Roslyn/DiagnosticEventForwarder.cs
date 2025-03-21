using System.Composition;
using System.Threading;
using System.Threading.Tasks;
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

        public ValueTask ForwardAsync(DiagnosticMessage message, CancellationToken cancellationToken = default) =>
            _emitter.EmitAsync(EventTypes.Diagnostic, message, cancellationToken);

        public async Task BackgroundDiagnosticsStatusAsync(BackgroundDiagnosticStatus status, int numberProjects, int numberFiles, int numberFilesRemaining, CancellationToken cancellationToken = default)
        {
            // New type of background diagnostic event, allows more control of visualization in clients:
            await _emitter.EmitAsync(EventTypes.BackgroundDiagnosticStatus, new BackgroundDiagnosticStatusMessage
            {
                Status = status,
                NumberProjects = numberProjects,
                NumberFilesTotal = numberFiles,
                NumberFilesRemaining = numberFilesRemaining
            }, cancellationToken);

            // Old type of event emitted as a shim for older clients:
            double percentComplete = 0;
            if (numberFiles > 0 && numberFiles > numberFilesRemaining)
            {
                percentComplete = numberFiles <= 0
                    ? 100
                    : (numberFiles - numberFilesRemaining) / (double)numberFiles;
            }

            await _emitter.EmitAsync(EventTypes.ProjectDiagnosticStatus, new ProjectDiagnosticStatusMessage
            {
                // There is no current project file being analyzed anymore since all the analysis
                // executes concurrently, but we have to supply some value for the ProjectFilePath
                // property for clients that only know about this event. In VS Code the following
                // displays nicely as "Analyzing (24%)".
                ProjectFilePath = $"({percentComplete:P0})",
                Status = status == BackgroundDiagnosticStatus.Finished ?
                    ProjectDiagnosticStatus.Ready :
                    ProjectDiagnosticStatus.Started
            }, cancellationToken);
        }
    }
}
