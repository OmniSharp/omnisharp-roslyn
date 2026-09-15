using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild.Notification
{
    public class ProjectLoadedEventArgs
    {
        public ProjectId Id { get; }
        public Guid SessionId { get; }
        public ImmutableArray<MSBuildDiagnostic> Diagnostics { get; }
        public bool IsReload { get; }
        public IEnumerable<string> References { get; }
        public ImmutableArray<string> SourceFiles { get; }
        public ImmutableArray<string> ContentFiles { get; }
        public ImmutableArray<string> TargetFrameworks { get; }
        public ImmutableArray<string> ProjectCapabilities { get; }
        public string ProjectFilePath { get; }
        public OutputKind OutputKind { get; }
        public bool IsSdkStyleProject { get; }
        public bool ProjectIdIsDefinedInSolution { get; }
        public SemanticVersion SdkVersion { get; }

        internal ProjectLoadedEventArgs(
            ProjectId id,
            Guid sessionId,
            ProjectFileInfo project,
            ImmutableArray<MSBuildDiagnostic> diagnostics,
            bool isReload,
            bool projectIdIsDefinedInSolution,
            ImmutableArray<string> sourceFiles,
            SemanticVersion sdkVersion,
            IEnumerable<string> references = null)
        {
            Id = id;
            SessionId = sessionId;
            Diagnostics = diagnostics;
            IsReload = isReload;
            ProjectIdIsDefinedInSolution = projectIdIsDefinedInSolution;
            References = references;
            SourceFiles = sourceFiles;
            SdkVersion = sdkVersion;
            ContentFiles = project.ContentFilePaths;
            TargetFrameworks = project.TargetFrameworks;
            ProjectCapabilities = project.ProjectCapabilities;
            ProjectFilePath = project.FilePath;
            OutputKind = project.OutputKind;
            IsSdkStyleProject = project.TargetFrameworks.Length > 0 ||
                project.ProjectCapabilities.Contains("CPS", StringComparer.OrdinalIgnoreCase);
        }
    }
}
