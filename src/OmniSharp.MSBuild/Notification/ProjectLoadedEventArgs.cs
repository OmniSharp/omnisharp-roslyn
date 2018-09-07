using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using OmniSharp.MSBuild.Logging;
using MSB = Microsoft.Build;

namespace OmniSharp.MSBuild.Notification
{
    public class ProjectLoadedEventArgs
    {
        public ProjectId Id { get; }
        public MSB.Execution.ProjectInstance ProjectInstance { get; }
        public ImmutableArray<MSBuildDiagnostic> Diagnostics { get; }
        public bool IsReload { get; }

        public ProjectLoadedEventArgs(
            ProjectId id,
            MSB.Execution.ProjectInstance projectInstance,
            ImmutableArray<MSBuildDiagnostic> diagnostics,
            bool isReload)
        {
            Id = id;
            ProjectInstance = projectInstance;
            Diagnostics = diagnostics;
            IsReload = isReload;
        }
    }
}
