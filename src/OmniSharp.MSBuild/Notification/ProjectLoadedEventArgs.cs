using System;
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
        public MSB.Evaluation.Project Project { get; }
        public Guid SessionId { get; }
        public MSB.Execution.ProjectInstance ProjectInstance { get; }
        public ImmutableArray<MSBuildDiagnostic> Diagnostics { get; }
        public bool IsReload { get; }
        public IEnumerable<string> References { get; }
        public ImmutableArray<string> SourceFiles { get; }
        public bool ProjectIdIsDefinedInSolution { get; }
        public SemanticVersion SdkVersion { get; }

        public ProjectLoadedEventArgs(
            ProjectId id,
            MSB.Evaluation.Project project,
            Guid sessionId,
            MSB.Execution.ProjectInstance projectInstance,
            ImmutableArray<MSBuildDiagnostic> diagnostics,
            bool isReload,
            bool projectIdIsDefinedInSolution,
            ImmutableArray<string> sourceFiles,
            SemanticVersion sdkVersion,
            IEnumerable<string> references = null)
        {
            Id = id;
            Project = project;
            SessionId = sessionId;
            ProjectInstance = projectInstance;
            Diagnostics = diagnostics;
            IsReload = isReload;
            ProjectIdIsDefinedInSolution = projectIdIsDefinedInSolution;
            References = references;
            SourceFiles = sourceFiles;
            SdkVersion = sdkVersion;
        }
    }
}
