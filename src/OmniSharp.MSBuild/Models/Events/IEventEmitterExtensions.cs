using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using OmniSharp.Eventing;
using OmniSharp.MSBuild.Logging;

namespace OmniSharp.MSBuild.Models.Events
{
    internal static class IEventEmitterExtensions
    {
        public static void MSBuildProjectDiagnostics(this IEventEmitter eventEmitter, string projectFilePath, ImmutableArray<MSBuildDiagnostic> diagnostics)
        {
            eventEmitter.Emit(MSBuildProjectDiagnosticsEvent.Id, new MSBuildProjectDiagnosticsEvent()
            {
                FileName = projectFilePath,
                Warnings = SelectMessages(diagnostics, MSBuildDiagnosticSeverity.Warning),
                Errors = SelectMessages(diagnostics, MSBuildDiagnosticSeverity.Error)
            });
        }

        private static IEnumerable<MSBuildDiagnosticsMessage> SelectMessages(ImmutableArray<MSBuildDiagnostic> diagnostics, MSBuildDiagnosticSeverity severity)
            => diagnostics
                .Where(d => d.Severity == severity)
                .Select(MSBuildDiagnosticsMessage.FromDiagnostic);
    }
}
