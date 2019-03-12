using System.Collections.Generic;
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
        public IEnumerable<string> References { get; }
        public bool HasProjectIdGeneratedFromSolution { get; }

        public ProjectLoadedEventArgs(
            ProjectId id,
            MSB.Execution.ProjectInstance projectInstance,
            ImmutableArray<MSBuildDiagnostic> diagnostics,
            bool isReload,
            bool hasProjectIdGeneratedFromSolution,
            IEnumerable<string> references = null)
        {
            Id = id;
            ProjectInstance = projectInstance;
            Diagnostics = diagnostics;
            IsReload = isReload;
            HasProjectIdGeneratedFromSolution = hasProjectIdGeneratedFromSolution;
            References = references;
        }
    }
}
