using System.Collections.Generic;
using System.Linq;
using OmniSharp.Eventing;

namespace OmniSharp.MSBuild.Models.Events
{
    internal static class IEventEmitterExtensions
    {
        public static void MSBuildProjectDiagnostics(this IEventEmitter eventEmitter, string projectFilePath, IEnumerable<MSBuildDiagnosticsMessage> diagnostics)
        {
            eventEmitter.Emit(MSBuildProjectDiagnosticsEvent.Id, new MSBuildProjectDiagnosticsEvent()
            {
                FileName = projectFilePath,
                Warnings = diagnostics.Where(d => d.LogLevel == "Warning"),
                Errors = diagnostics.Where(d => d.LogLevel == "Error"),
            });
        }
    }
}
