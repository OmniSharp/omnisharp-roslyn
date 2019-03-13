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
        public ImmutableArray<string> SourceFiles { get; }
        public bool ProjectIdIsDefinedInSolution { get; }

        public ProjectLoadedEventArgs(
            ProjectId id,
            MSB.Execution.ProjectInstance projectInstance,
            ImmutableArray<MSBuildDiagnostic> diagnostics,
            bool isReload,
            bool projectIdIsDefinedInSolution,
            ImmutableArray<string> sourceFiles,
            IEnumerable<string> references = null)
        {
            Id = id;
            ProjectInstance = projectInstance;
            Diagnostics = diagnostics;
            IsReload = isReload;
            ProjectIdIsDefinedInSolution = projectIdIsDefinedInSolution;
            References = references;
            SourceFiles = sourceFiles;
        }
    }
}
