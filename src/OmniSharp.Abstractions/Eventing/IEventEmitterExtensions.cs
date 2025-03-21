using System;
using System.Collections.Generic;
using OmniSharp.Models.Events;
using OmniSharp.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Eventing
{
    public static class IEventEmitterExtensions
    {
        public static ValueTask ErrorAsync(this IEventEmitter emitter, Exception ex, string fileName = null, CancellationToken cancellationToken = default) =>
            emitter.EmitAsync(
                EventTypes.Error,
                new ErrorMessage { FileName = fileName, Text = ex.ToString() },
                cancellationToken);

        public static ValueTask RestoreStartedAsync(this IEventEmitter emitter, string projectPath, CancellationToken cancellationToken = default) =>
            emitter.EmitAsync(
                EventTypes.PackageRestoreStarted,
                new PackageRestoreMessage { FileName = projectPath },
                cancellationToken);

        public static ValueTask RestoreFinishedAsync(this IEventEmitter emitter, string projectPath, bool succeeded, CancellationToken cancellationToken = default) =>
            emitter.EmitAsync(
                EventTypes.PackageRestoreFinished,
                new PackageRestoreMessage
                {
                    FileName = projectPath,
                    Succeeded = succeeded
                },
                cancellationToken);

        public static ValueTask UnresolvedDependenciesAsync(this IEventEmitter emitter, string projectFilePath, IEnumerable<PackageDependency> unresolvedDependencies, CancellationToken cancellationToken = default) =>
            emitter.EmitAsync(
                EventTypes.UnresolvedDependencies,
                new UnresolvedDependenciesMessage
                {
                    FileName = projectFilePath,
                    UnresolvedDependencies = unresolvedDependencies
                },
                cancellationToken);

        public static ValueTask ProjectLoadStartedAsync(this IEventEmitter emitter, string projectPath, CancellationToken cancellationToken = default) =>
            emitter.EmitAsync(
                    EventTypes.ProjectLoadStarted,
                    projectPath,
                    cancellationToken);

        public static ValueTask ProjectLoadFinishedAsync(this IEventEmitter emitter, string projectPath, CancellationToken cancellationToken = default) =>
            emitter.EmitAsync(
                    EventTypes.ProjectLoadFinished,
                    projectPath,
                    cancellationToken);

        public static async Task ProjectInformationAsync(this IEventEmitter emitter,
                                              HashedString projectId,
                                              HashedString sessionId,
                                              int outputKind,
                                              IEnumerable<string> projectCapabilities,
                                              IEnumerable<string> targetFrameworks,
                                              HashedString sdkVersion,
                                              IEnumerable<HashedString> references,
                                              IEnumerable<HashedString> fileExtensions,
                                              IEnumerable<int> fileCounts,
                                              bool sdkStyleProject,
                                              CancellationToken cancellationToken = default)
        {
            var projectConfiguration = new ProjectConfigurationMessage()
            {
                ProjectCapabilities = projectCapabilities,
                TargetFrameworks = targetFrameworks,
                SdkVersion = sdkVersion.Value,
                OutputKind = outputKind,
                ProjectId = projectId.Value,
                SessionId = sessionId.Value,
                References = references.Select(hashed => hashed.Value),
                FileExtensions = fileExtensions.Select(hashed => hashed.Value),
                FileCounts = fileCounts,
                SdkStyleProject = sdkStyleProject
            };

            await emitter.EmitAsync(
                EventTypes.ProjectConfiguration,
                projectConfiguration,
                cancellationToken);
        }
    }
}
