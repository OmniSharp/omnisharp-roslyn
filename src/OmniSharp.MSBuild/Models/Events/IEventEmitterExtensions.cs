using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Eventing;
using OmniSharp.MSBuild.Logging;

namespace OmniSharp.MSBuild.Models.Events
{
    internal static class IEventEmitterExtensions
    {
        public static ValueTask MSBuildProjectDiagnosticsAsync(this IEventEmitter eventEmitter, string projectFilePath, ImmutableArray<MSBuildDiagnostic> diagnostics, CancellationToken cancellationToken = default) =>
            eventEmitter.EmitAsync(MSBuildProjectDiagnosticsEvent.Id, new MSBuildProjectDiagnosticsEvent()
            {
                FileName = projectFilePath,
                Warnings = SelectMessages(diagnostics, MSBuildDiagnosticSeverity.Warning),
                Errors = SelectMessages(diagnostics, MSBuildDiagnosticSeverity.Error)
            }, cancellationToken);

        private static IEnumerable<MSBuildDiagnosticsMessage> SelectMessages(ImmutableArray<MSBuildDiagnostic> diagnostics, MSBuildDiagnosticSeverity severity)
            => diagnostics
                .Where(d => d.Severity == severity)
                .Select(MSBuildDiagnosticsMessage.FromDiagnostic);
    }
}
